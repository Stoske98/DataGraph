using System;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace DataGraph.OneDrive.Auth
{
    /// <summary>
    /// Client credentials (App-Only) token flow for Microsoft Graph API.
    /// Uses client_id + client_secret to obtain an application-level
    /// access token without user interaction.
    /// Requires Azure AD App Registration with Application permissions
    /// (Files.Read.All or Sites.Read.All), not Delegated permissions.
    /// Tenant must be a specific tenant ID, not "common".
    /// </summary>
    internal static class OneDriveAppOnlyFlow
    {
        private const string TokenEndpoint =
            "https://login.microsoftonline.com/{0}/oauth2/v2.0/token";

        private const string Scope = "https://graph.microsoft.com/.default";

        private static string _cachedAccessToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        /// <summary>
        /// Obtains an access token using client credentials flow.
        /// Caches the token until it expires.
        /// </summary>
        internal static async Task<Result<string>> GetAccessTokenAsync(
            string clientId, string tenantId, string clientSecret,
            CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken)
                && DateTime.UtcNow < _tokenExpiry)
                return Result<string>.Success(_cachedAccessToken);

            if (string.IsNullOrEmpty(clientId))
                return Result<string>.Failure(
                    "OneDrive Client ID is required for App-Only auth.");

            if (string.IsNullOrEmpty(clientSecret))
                return Result<string>.Failure(
                    "OneDrive Client Secret is required for App-Only auth.");

            if (string.IsNullOrEmpty(tenantId) || tenantId == "common")
                return Result<string>.Failure(
                    "A specific Tenant ID is required for App-Only auth. " +
                    "'common' is not supported with client credentials flow.");

            var url = string.Format(TokenEndpoint, tenantId);

            var form = new WWWForm();
            form.AddField("client_id", clientId);
            form.AddField("client_secret", clientSecret);
            form.AddField("grant_type", "client_credentials");
            form.AddField("scope", Scope);

            try
            {
                using var request = UnityWebRequest.Post(url, form);
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(50, ct);
                }

                if (request.result != UnityWebRequest.Result.Success)
                    return Result<string>.Failure(
                        $"App-Only token request failed (HTTP {request.responseCode}): " +
                        request.downloadHandler.text);

                var response = JsonUtility.FromJson<TokenResponse>(
                    request.downloadHandler.text);

                if (string.IsNullOrEmpty(response.access_token))
                    return Result<string>.Failure(
                        "Token response contains no access_token.");

                _cachedAccessToken = response.access_token;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(
                    response.expires_in > 0 ? response.expires_in - 60 : 3540);

                return Result<string>.Success(_cachedAccessToken);
            }
            catch (OperationCanceledException)
            {
                return Result<string>.Failure("Token request was cancelled.");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(
                    $"App-Only token request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the cached access token.
        /// </summary>
        internal static void ClearCache()
        {
            _cachedAccessToken = null;
            _tokenExpiry = DateTime.MinValue;
        }

        [Serializable]
        private struct TokenResponse
        {
            public string access_token;
            public int expires_in;
        }
    }
}
