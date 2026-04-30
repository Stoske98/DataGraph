using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DataGraph.GoogleSheets.Auth
{
    /// <summary>
    /// OAuth 2.0 authorization code flow for private Google Sheets.
    /// Opens the system browser for user consent, listens on localhost
    /// for the callback, exchanges code for tokens, and auto-refreshes.
    /// </summary>
    internal sealed class OAuthStrategy : IAuthStrategy
    {
        private const string TokenPrefsKey = "DataGraph_Google_OAuthToken";
        private const string ClientIdPrefsKey = "DataGraph_Google_OAuthClientId";
        private const string ClientSecretPrefsKey = "DataGraph_Google_OAuthClientSecret";

        private const string Scope = "https://www.googleapis.com/auth/spreadsheets.readonly";
        private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

        public string MethodId => "OAuth";
        public string DisplayName => "OAuth 2.0 (private sheets)";
        public bool RequiresInteractiveAuth => true;

        public bool IsConfigured
        {
            get
            {
                if (string.IsNullOrEmpty(GetClientId()))
                    return false;
                var token = LoadToken();
                return token != null && (!token.IsExpired || token.CanRefresh);
            }
        }

        public void SetClientCredentials(string clientId, string clientSecret)
        {
            EditorPrefs.SetString(ClientIdPrefsKey, clientId ?? "");
            EditorPrefs.SetString(ClientSecretPrefsKey, clientSecret ?? "");
        }

        public string GetClientId() => EditorPrefs.GetString(ClientIdPrefsKey, "");
        private string GetClientSecret() => EditorPrefs.GetString(ClientSecretPrefsKey, "");

        public async Task<Result<bool>> AuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            var clientId = GetClientId();
            if (string.IsNullOrEmpty(clientId))
                return Result<bool>.Failure(
                    "OAuth Client ID is not configured. Set it in DataGraph settings.");

            try
            {
                var (codeResult, redirectUri) = await ListenForAuthCodeAsync(clientId, cancellationToken);
                if (codeResult.IsFailure)
                    return Result<bool>.Failure(codeResult.Error);

                var tokenResult = await ExchangeCodeAsync(
                    clientId, GetClientSecret(), codeResult.Value, redirectUri, cancellationToken);
                if (tokenResult.IsFailure)
                    return Result<bool>.Failure(tokenResult.Error);

                SaveToken(tokenResult.Value);
                return Result<bool>.Success(true);
            }
            catch (OperationCanceledException)
            {
                return Result<bool>.Failure("Authorization was cancelled.");
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure($"Authorization failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthCredentials>> GetCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            var token = LoadToken();
            if (token == null)
                return Result<AuthCredentials>.Failure(
                    "Not authenticated. Please sign in to Google.");

            if (!token.IsExpired)
                return Result<AuthCredentials>.Success(
                    AuthCredentials.FromBearerToken(token.AccessToken));

            if (!token.CanRefresh)
                return Result<AuthCredentials>.Failure(
                    "Token expired with no refresh token. Please sign in again.");

            var refreshed = await RefreshTokenAsync(token, cancellationToken);
            if (refreshed.IsFailure)
                return Result<AuthCredentials>.Failure(refreshed.Error);

            return Result<AuthCredentials>.Success(
                AuthCredentials.FromBearerToken(refreshed.Value.AccessToken));
        }

        public void SignOut()
        {
            EditorPrefs.DeleteKey(TokenPrefsKey);
        }

        private async Task<(Result<string> result, string redirectUri)> ListenForAuthCodeAsync(
            string clientId, CancellationToken cancellationToken)
        {
            var port = FindAvailablePort();
            var redirectUri = $"http://localhost:{port}/";
            var state = Guid.NewGuid().ToString("N");

            var authUrl = $"{AuthEndpoint}" +
                          $"?client_id={Uri.EscapeDataString(clientId)}" +
                          $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                          $"&response_type=code" +
                          $"&scope={Uri.EscapeDataString(Scope)}" +
                          $"&access_type=offline" +
                          $"&prompt=consent" +
                          $"&state={state}";

            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);

            try
            {
                listener.Start();
                Application.OpenURL(authUrl);

                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(
                    contextTask,
                    Task.Delay(Timeout.Infinite, cancellationToken));

                if (completed != contextTask)
                    return (Result<string>.Failure("Authorization timed out or was cancelled."), redirectUri);

                var context = contextTask.Result;
                var code = context.Request.QueryString["code"];
                var error = context.Request.QueryString["error"];

                var html = !string.IsNullOrEmpty(code)
                    ? "<html><body><h2>Authorization successful!</h2><p>You can close this window and return to Unity.</p></body></html>"
                    : "<html><body><h2>Authorization failed.</h2><p>Please try again in Unity.</p></body></html>";

                var buffer = System.Text.Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(
                    buffer, 0, buffer.Length, cancellationToken);
                context.Response.Close();

                if (!string.IsNullOrEmpty(error))
                    return (Result<string>.Failure($"Authorization denied: {error}"), redirectUri);
                if (context.Request.QueryString["state"] != state)
                    return (Result<string>.Failure("OAuth state mismatch — possible CSRF attempt."), redirectUri);
                if (string.IsNullOrEmpty(code))
                    return (Result<string>.Failure("No authorization code received."), redirectUri);

                return (Result<string>.Success(code), redirectUri);
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        private static int FindAvailablePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static async Task<Result<OAuthToken>> ExchangeCodeAsync(
            string clientId, string clientSecret, string code,
            string redirectUri, CancellationToken cancellationToken)
        {
            var form = new WWWForm();
            form.AddField("client_id", clientId);
            form.AddField("client_secret", clientSecret);
            form.AddField("code", code);
            form.AddField("redirect_uri", redirectUri);
            form.AddField("grant_type", "authorization_code");

            return await PostTokenRequestAsync(form, null, cancellationToken);
        }

        private async Task<Result<OAuthToken>> RefreshTokenAsync(
            OAuthToken existing, CancellationToken cancellationToken)
        {
            var form = new WWWForm();
            form.AddField("client_id", GetClientId());
            form.AddField("client_secret", GetClientSecret());
            form.AddField("refresh_token", existing.RefreshToken);
            form.AddField("grant_type", "refresh_token");

            var result = await PostTokenRequestAsync(form, existing.RefreshToken, cancellationToken);
            if (result.IsSuccess)
                SaveToken(result.Value);
            return result;
        }

        private static async Task<Result<OAuthToken>> PostTokenRequestAsync(
            WWWForm form, string existingRefreshToken,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Post(TokenEndpoint, form);
            await request.SendAsync(cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
                return Result<OAuthToken>.Failure($"Token request failed: {request.error}");

            var response = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
            if (string.IsNullOrEmpty(response.access_token))
                return Result<OAuthToken>.Failure("Empty access token in response.");

            var token = new OAuthToken
            {
                AccessToken = response.access_token,
                RefreshToken = !string.IsNullOrEmpty(response.refresh_token)
                    ? response.refresh_token
                    : existingRefreshToken,
                ExpiryTime = DateTime.UtcNow.AddSeconds(response.expires_in)
            };

            return Result<OAuthToken>.Success(token);
        }

        private void SaveToken(OAuthToken token)
        {
            EditorPrefs.SetString(TokenPrefsKey, JsonUtility.ToJson(token));
        }

        private OAuthToken LoadToken()
        {
            if (!EditorPrefs.HasKey(TokenPrefsKey))
                return null;
            var json = EditorPrefs.GetString(TokenPrefsKey, "");
            if (string.IsNullOrEmpty(json))
                return null;
            try { return JsonUtility.FromJson<OAuthToken>(json); }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"DataGraph (Google OAuth): failed to deserialize stored token: {ex.Message}. " +
                    "Token will be ignored — please sign in again.");
                return null;
            }
        }

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
        }
    }
}
