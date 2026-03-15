using System.Threading;
using System.Threading.Tasks;
using DataGraph.Runtime;

namespace DataGraph.GoogleSheets.Auth
{
    /// <summary>
    /// Abstraction for Google Sheets authentication methods.
    /// V1: ApiKeyStrategy, OAuthStrategy.
    /// V2: ServiceAccountStrategy.
    /// </summary>
    internal interface IAuthStrategy
    {
        /// <summary>
        /// Unique identifier for this auth method.
        /// </summary>
        string MethodId { get; }

        /// <summary>
        /// Human-readable name for UI display.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this strategy is currently configured and ready.
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Whether this strategy requires interactive user action to authenticate.
        /// False for API Key (just needs a key string).
        /// True for OAuth (browser login).
        /// </summary>
        bool RequiresInteractiveAuth { get; }

        /// <summary>
        /// Runs interactive authentication if needed. No-op for API Key.
        /// </summary>
        Task<Result<bool>> AuthenticateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies authentication to a request URL.
        /// For API Key: appends ?key=... query parameter.
        /// For OAuth: returns the access token for Authorization header.
        /// </summary>
        Task<Result<AuthCredentials>> GetCredentialsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears stored credentials.
        /// </summary>
        void SignOut();
    }

    /// <summary>
    /// Credentials produced by an auth strategy.
    /// Either a query parameter (API Key) or a bearer token (OAuth).
    /// </summary>
    internal sealed class AuthCredentials
    {
        private AuthCredentials() { }

        /// <summary>
        /// API Key to append as query parameter.
        /// </summary>
        public string ApiKey { get; private set; }

        /// <summary>
        /// Bearer token for Authorization header.
        /// </summary>
        public string BearerToken { get; private set; }

        /// <summary>
        /// Whether this uses API Key auth (vs bearer token).
        /// </summary>
        public bool IsApiKey => !string.IsNullOrEmpty(ApiKey);

        public static AuthCredentials FromApiKey(string key) => new() { ApiKey = key };
        public static AuthCredentials FromBearerToken(string token) => new() { BearerToken = token };
    }
}
