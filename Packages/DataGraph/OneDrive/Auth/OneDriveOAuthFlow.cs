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

namespace DataGraph.OneDrive.Auth
{
    /// <summary>
    /// OAuth 2.0 Authorization Code flow with PKCE against Microsoft Identity Platform.
    /// Uses a local HTTP listener on a random port as redirect URI.
    /// No client secret — public client model for desktop/editor apps.
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
        /// Starts interactive sign-in. Opens browser, waits for redirect.
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
            _ = RunFlowAsync(clientId, tenantId, _cts.Token)
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                        Debug.LogError(
                            "[DataGraph] OneDrive auth flow crashed: " +
                            t.Exception.GetBaseException().Message);
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Refreshes access token using stored refresh token.
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
            await request.SendAsync(ct);

            if (request.result != UnityWebRequest.Result.Success)
                return Result<string>.Failure(
                    $"Token refresh failed: HTTP {request.responseCode}");

            try
            {
                var response = JsonUtility.FromJson<TokenResponse>(
                    request.downloadHandler.text);

                if (!string.IsNullOrEmpty(response.refresh_token))
                    OneDriveCredentials.RefreshToken = response.refresh_token;

                return Result<string>.Success(response.access_token);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(
                    $"Failed to parse refresh response: {ex.Message}");
            }
        }

        private static async Task RunFlowAsync(string clientId, string tenantId,
            CancellationToken ct)
        {
            var (codeVerifier, codeChallenge) = GeneratePkce();

            var port = FindAvailablePort();
            var redirectUri = $"http://localhost:{port}/";

            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);

            try { listener.Start(); }
            catch (HttpListenerException ex)
            {
                Debug.LogError(
                    $"[DataGraph] HTTP listener failed on port {port}: {ex.Message}");
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
                var code = query["code"];
                var error = query["error"];

                var isError = !string.IsNullOrEmpty(error);
                await SendResponseAsync(context, isError
                    ? $"Error: {error}. {query["error_description"]}"
                    : "Success! You can close this window.", ct);

                if (isError) { Debug.LogError($"[DataGraph] OneDrive: {error}"); return; }
                if (query["state"] != state) { Debug.LogError("[DataGraph] OneDrive: state mismatch."); return; }
                if (string.IsNullOrEmpty(code)) { Debug.LogError("[DataGraph] OneDrive: no code."); return; }

                var tokenResult = await ExchangeCodeAsync(
                    clientId, tenantId, code, redirectUri, codeVerifier, ct);

                if (tokenResult.IsSuccess)
                {
                    OneDriveCredentials.RefreshToken = tokenResult.Value.refresh_token;
                    Debug.Log("[DataGraph] OneDrive authentication successful.");
                }
                else
                    Debug.LogError($"[DataGraph] Token exchange failed: {tokenResult.Error}");
            }
            catch (OperationCanceledException) { Debug.Log("[DataGraph] OneDrive auth cancelled."); }
            finally { listener.Stop(); listener.Close(); }
        }

        private static async Task<HttpListenerContext> WaitForCallbackAsync(
            HttpListener listener, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            using var reg = ct.Register(() => tcs.TrySetResult(true));
            var task = listener.GetContextAsync();
            var completed = await Task.WhenAny(task, tcs.Task);
            if (completed == tcs.Task) throw new OperationCanceledException(ct);
            return await task;
        }

        private static async Task SendResponseAsync(
            HttpListenerContext context, string message, CancellationToken ct)
        {
            var html = "<!DOCTYPE html><html><body style='font-family:sans-serif;" +
                "text-align:center;padding-top:80px'><p>" + message + "</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);
            context.Response.Close();
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
            await request.SendAsync(ct);

            if (request.result != UnityWebRequest.Result.Success)
                return Result<TokenResponse>.Failure(
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");

            try
            {
                var response = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                return string.IsNullOrEmpty(response.refresh_token)
                    ? Result<TokenResponse>.Failure("No refresh token. Ensure offline_access scope.")
                    : Result<TokenResponse>.Success(response);
            }
            catch (Exception ex) { return Result<TokenResponse>.Failure($"Parse error: {ex.Message}"); }
        }

        private static (string verifier, string challenge) GeneratePkce()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            var verifier = Base64Url.Encode(bytes);
            using var sha = SHA256.Create();
            return (verifier, Base64Url.Encode(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier))));
        }

        private static int FindAvailablePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start(); var port = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
            return port;
        }

        [Serializable]
        internal struct TokenResponse
        {
            public string access_token;
            public string refresh_token;
        }
    }
}
