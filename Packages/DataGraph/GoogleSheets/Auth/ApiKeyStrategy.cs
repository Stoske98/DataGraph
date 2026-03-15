using System.Threading;
using System.Threading.Tasks;
using DataGraph.Runtime;
using UnityEditor;

namespace DataGraph.GoogleSheets.Auth
{
    /// <summary>
    /// Authentication via Google API Key. Simplest method — no login required.
    /// Works with sheets that are public or shared via link.
    /// API Key is stored in EditorPrefs.
    /// </summary>
    internal sealed class ApiKeyStrategy : IAuthStrategy
    {
        private const string PrefsKey = "DataGraph_Google_ApiKey";

        public string MethodId => "ApiKey";
        public string DisplayName => "API Key (public sheets)";
        public bool RequiresInteractiveAuth => false;

        public bool IsConfigured => !string.IsNullOrEmpty(GetStoredKey());

        /// <summary>
        /// Stores the API key.
        /// </summary>
        public void SetApiKey(string apiKey)
        {
            EditorPrefs.SetString(PrefsKey, apiKey ?? "");
        }

        /// <summary>
        /// Gets the stored API key.
        /// </summary>
        public string GetStoredKey()
        {
            return EditorPrefs.GetString(PrefsKey, "");
        }

        public Task<Result<bool>> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            if (IsConfigured)
                return Task.FromResult(Result<bool>.Success(true));
            return Task.FromResult(Result<bool>.Failure(
                "API Key is not set. Enter your Google API Key in DataGraph settings."));
        }

        public Task<Result<AuthCredentials>> GetCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            var key = GetStoredKey();
            if (string.IsNullOrEmpty(key))
                return Task.FromResult(Result<AuthCredentials>.Failure("API Key is not set."));

            return Task.FromResult(Result<AuthCredentials>.Success(
                AuthCredentials.FromApiKey(key)));
        }

        public void SignOut()
        {
            EditorPrefs.DeleteKey(PrefsKey);
        }
    }
}
