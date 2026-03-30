using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DataGraph.Editor
{
    /// <summary>
    /// Executes OAuth 2.0 Authorization Code flow with PKCE against
    /// Microsoft Identity Platform. Uses a local HTTP listener on a
    /// random port as redirect URI. No client secret required —
    /// public client model appropriate for desktop/editor apps.
    /// Stores refresh token in <see cref="DataGraphCredentials.OneDrive"/>.
    /// </summary>
    internal static class OneDriveOAuthFlow
    {
        private const string AuthorizeEndpoint =
            "https://login.microsoftonline.com/{0}/oauth2/v2.0/authorize";

        private const string TokenEndpoint =
            "https://login.microsoftonline.com/{0}/oauth2/v2.0/token";

        private const string Scopes = "Files.Read Files.Read.All offline_access";

        private static CancellationTokenSource _cts;

        /// <summary>
        /// Starts the interactive sign-in flow. Opens the default browser.
        /// On success, stores the refresh token in DataGraphCredentials.
        /// </summary>
        internal static void Start(string clientId, string tenantId)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                Debug.LogError("[DataGraph] OneDrive Client ID is required.");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            RunFlowAsync(clientId, tenantId, _cts.Token);
        }

        /// <summary>
        /// Refreshes an access token using a stored refresh token.
        /// Called by OneDriveFetcher before API requests and on 401 retry.
        /// Updates the stored refresh token if the server rotates it.
        /// </summary>
        internal static async Task<Result<string>> RefreshAccessTokenAsync(
            string clientId, string tenantId, string refreshToken,
            CancellationToken ct = default)
        {
            var url = string.Format(TokenEndpoint, tenantId);

            var form = new WWWForm();
            form.AddField("client_id", clientId);
            form.AddField("grant_type", "refresh_token");
            form.AddField("refresh_token", refreshToken);
            form.AddField("scope", Scopes);

            using var request = UnityWebRequest.Post(url, form);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return Result<string>.Failure(
                    $"Token refresh failed: HTTP {request.responseCode}");
            }

            try
            {
                var response = JsonUtility.FromJson<TokenResponse>(
                    request.downloadHandler.text);

                if (!string.IsNullOrEmpty(response.refresh_token))
                    DataGraphCredentials.OneDrive.RefreshToken = response.refresh_token;

                return Result<string>.Success(response.access_token);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(
                    $"Failed to parse refresh response: {ex.Message}");
            }
        }

        private static async void RunFlowAsync(string clientId, string tenantId,
            CancellationToken ct)
        {
            var (codeVerifier, codeChallenge) = GeneratePkce();

            var port = FindAvailablePort();
            var redirectUri = $"http://localhost:{port}/";

            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Debug.LogError(
                    $"[DataGraph] Failed to start HTTP listener on port {port}: {ex.Message}");
                return;
            }

            var state = Guid.NewGuid().ToString("N");
            var authorizeUrl = string.Format(AuthorizeEndpoint, tenantId)
                + $"?client_id={Uri.EscapeDataString(clientId)}"
                + "&response_type=code"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_mode=query"
                + $"&scope={Uri.EscapeDataString(Scopes)}"
                + $"&state={state}"
                + $"&code_challenge={codeChallenge}"
                + "&code_challenge_method=S256";

            Application.OpenURL(authorizeUrl);
            Debug.Log("[DataGraph] Opened browser for OneDrive sign-in.");

            try
            {
                var context = await WaitForCallbackAsync(listener, ct);

                var query = context.Request.QueryString;
                var returnedState = query["state"];
                var code = query["code"];
                var error = query["error"];

                var isError = !string.IsNullOrEmpty(error);
                var html = BuildResponseHtml(
                    isError ? "Error" : "Success",
                    isError
                        ? $"Authentication failed: {error}. {query["error_description"]}"
                        : "You can close this window and return to Unity.");

                var buffer = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(
                    buffer, 0, buffer.Length, ct);
                context.Response.Close();

                if (isError)
                {
                    Debug.LogError($"[DataGraph] OneDrive auth error: {error}");
                    return;
                }

                if (returnedState != state)
                {
                    Debug.LogError("[DataGraph] OneDrive auth: state mismatch.");
                    return;
                }

                if (string.IsNullOrEmpty(code))
                {
                    Debug.LogError("[DataGraph] OneDrive auth: no code received.");
                    return;
                }

                var tokenResult = await ExchangeCodeAsync(
                    clientId, tenantId, code, redirectUri, codeVerifier, ct);

                if (tokenResult.IsSuccess)
                {
                    DataGraphCredentials.OneDrive.RefreshToken =
                        tokenResult.Value.refresh_token;
                    Debug.Log("[DataGraph] OneDrive authentication successful.");
                }
                else
                {
                    Debug.LogError(
                        $"[DataGraph] Token exchange failed: {tokenResult.Error}");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[DataGraph] OneDrive auth flow cancelled.");
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        private static async Task<HttpListenerContext> WaitForCallbackAsync(
            HttpListener listener, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            using var reg = ct.Register(() => tcs.TrySetResult(true));

            var listenerTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(listenerTask, tcs.Task);

            if (completed == tcs.Task)
                throw new OperationCanceledException(ct);

            return await listenerTask;
        }

        private static async Task<Result<TokenResponse>> ExchangeCodeAsync(
            string clientId, string tenantId, string code,
            string redirectUri, string codeVerifier, CancellationToken ct)
        {
            var url = string.Format(TokenEndpoint, tenantId);

            var form = new WWWForm();
            form.AddField("client_id", clientId);
            form.AddField("grant_type", "authorization_code");
            form.AddField("code", code);
            form.AddField("redirect_uri", redirectUri);
            form.AddField("code_verifier", codeVerifier);
            form.AddField("scope", Scopes);

            using var request = UnityWebRequest.Post(url, form);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return Result<TokenResponse>.Failure(
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");
            }

            try
            {
                var response = JsonUtility.FromJson<TokenResponse>(
                    request.downloadHandler.text);

                if (string.IsNullOrEmpty(response.refresh_token))
                {
                    return Result<TokenResponse>.Failure(
                        "No refresh token in response. " +
                        "Ensure offline_access scope is granted in Azure app.");
                }

                return Result<TokenResponse>.Success(response);
            }
            catch (Exception ex)
            {
                return Result<TokenResponse>.Failure(
                    $"Failed to parse token response: {ex.Message}");
            }
        }

        private static (string verifier, string challenge) GeneratePkce()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            var verifier = Base64UrlEncode(bytes);

            using var sha256 = SHA256.Create();
            var challengeBytes = sha256.ComputeHash(
                Encoding.ASCII.GetBytes(verifier));
            var challenge = Base64UrlEncode(challengeBytes);

            return (verifier, challenge);
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static int FindAvailablePort()
        {
            var listener = new System.Net.Sockets.TcpListener(
                IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string BuildResponseHtml(string title, string message)
        {
            return "<!DOCTYPE html>" +
                $"<html><head><title>DataGraph - {title}</title>" +
                "<style>body{font-family:sans-serif;text-align:center;" +
                "padding-top:80px}h1{color:#333}p{color:#666}</style></head>" +
                $"<body><h1>{title}</h1><p>{message}</p></body></html>";
        }

        /// <summary>
        /// Microsoft token endpoint response. Field names match JSON keys exactly
        /// for JsonUtility deserialization.
        /// </summary>
        [Serializable]
        internal struct TokenResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
        }
    }
}
