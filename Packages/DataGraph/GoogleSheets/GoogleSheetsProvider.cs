using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.Public;
using DataGraph.GoogleSheets.Auth;
using DataGraph.GoogleSheets.Fetch;
using DataGraph.Runtime;

namespace DataGraph.GoogleSheets
{
    /// <summary>
    /// ISheetProvider implementation for Google Sheets API v4.
    /// Supports multiple authentication methods through IAuthStrategy:
    /// V1: API Key (public sheets), OAuth 2.0 (private sheets).
    /// V2: Service Account.
    /// </summary>
    internal sealed class GoogleSheetsProvider : ISheetProvider
    {
        private readonly Dictionary<string, IAuthStrategy> _strategies = new();
        private readonly GoogleSheetsFetcher _fetcher = new();
        private readonly SheetDataCache _cache = new();
        private IAuthStrategy _activeStrategy;

        public GoogleSheetsProvider()
        {
            var apiKey = new ApiKeyStrategy();
            var oauth = new OAuthStrategy();

            _strategies[apiKey.MethodId] = apiKey;
            _strategies[oauth.MethodId] = oauth;

            if (apiKey.IsConfigured)
                _activeStrategy = apiKey;
            else if (oauth.IsConfigured)
                _activeStrategy = oauth;
        }

        public string ProviderId => "GoogleSheets";
        public string DisplayName => "Google Sheets";

        public bool IsAuthenticated => _activeStrategy?.IsConfigured ?? false;

        /// <summary>
        /// All registered auth strategies.
        /// </summary>
        public IReadOnlyDictionary<string, IAuthStrategy> Strategies => _strategies;

        /// <summary>
        /// The currently active auth strategy. Null if none configured.
        /// </summary>
        public IAuthStrategy ActiveStrategy => _activeStrategy;

        /// <summary>
        /// Sets the active authentication method by ID.
        /// </summary>
        public void SetActiveStrategy(string methodId)
        {
            if (_strategies.TryGetValue(methodId, out var strategy))
                _activeStrategy = strategy;
        }

        /// <summary>
        /// Configures and activates API Key authentication.
        /// </summary>
        public void ConfigureApiKey(string apiKey)
        {
            var strategy = (ApiKeyStrategy)_strategies["ApiKey"];
            strategy.SetApiKey(apiKey);
            _activeStrategy = strategy;
        }

        /// <summary>
        /// Configures OAuth credentials and runs interactive login.
        /// </summary>
        public async Task<Result<bool>> ConfigureOAuthAsync(
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken = default)
        {
            var strategy = (OAuthStrategy)_strategies["OAuth"];
            strategy.SetClientCredentials(clientId, clientSecret);
            _activeStrategy = strategy;
            return await strategy.AuthenticateAsync(cancellationToken);
        }

        public async Task<Result<RawTableData>> FetchAsync(
            SheetReference reference,
            CancellationToken cancellationToken = default)
        {
            return await FetchAsync(reference, forceRefresh: false, cancellationToken);
        }

        /// <summary>
        /// Fetches data with explicit cache control.
        /// </summary>
        public async Task<Result<RawTableData>> FetchAsync(
            SheetReference reference,
            bool forceRefresh,
            CancellationToken cancellationToken = default)
        {
            if (!forceRefresh)
            {
                var cached = _cache.Get(reference.SheetId);
                if (cached != null)
                    return Result<RawTableData>.Success(cached);
            }

            if (_activeStrategy == null)
                return Result<RawTableData>.Failure(
                    "No authentication method configured. " +
                    "Set an API Key or sign in with OAuth in DataGraph settings.");

            var credResult = await _activeStrategy.GetCredentialsAsync(cancellationToken);
            if (credResult.IsFailure)
                return Result<RawTableData>.Failure(credResult.Error);

            var idResult = SheetUrlParser.ExtractSpreadsheetId(reference.SheetId);
            if (idResult.IsFailure)
                return Result<RawTableData>.Failure(idResult.Error);

            var fetchResult = await _fetcher.FetchAsync(
                idResult.Value,
                credResult.Value,
                reference,
                cancellationToken,
                reference.Columns);

            if (fetchResult.IsSuccess)
                _cache.Set(reference.SheetId, fetchResult.Value);

            return fetchResult;
        }

        /// <summary>
        /// Clears all stored credentials and cache.
        /// </summary>
        public void SignOut()
        {
            foreach (var strategy in _strategies.Values)
                strategy.SignOut();
            _activeStrategy = null;
            _cache.Clear();
        }

        /// <summary>
        /// Invalidates cached data, forcing next fetch to call the API.
        /// </summary>
        public void InvalidateCache() => _cache.Clear();
    }
}
