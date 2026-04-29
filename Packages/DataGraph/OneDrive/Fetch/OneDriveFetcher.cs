using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.OneDrive.Auth;
using DataGraph.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace DataGraph.OneDrive.Fetch
{
    /// <summary>
    /// Fetches tabular data from Excel files on OneDrive/SharePoint
    /// via Microsoft Graph API v1.0. Converts usedRange JSON into
    /// RawTableData. Uses manual JSON parsing for the values 2D array
    /// because Unity's JsonUtility cannot deserialize jagged arrays.
    /// Supports both user-delegated (/me/drive) and app-only
    /// (/drives/{driveId}) access patterns.
    /// </summary>
    internal sealed class OneDriveFetcher
    {
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

        private readonly string _clientId;
        private readonly string _tenantId;
        private readonly bool _isAppOnly;
        private string _accessToken;

        internal OneDriveFetcher(string clientId, string tenantId, bool isAppOnly = false)
        {
            _clientId = clientId;
            _tenantId = tenantId;
            _isAppOnly = isAppOnly;
        }

        /// <summary>
        /// Fetches worksheet data from an Excel file on OneDrive.
        /// If columns is provided, fetches only those columns using
        /// individual range requests and merges results.
        /// </summary>
        internal async Task<Result<RawTableData>> FetchAsync(
            string itemDescriptor, string worksheetName,
            CancellationToken ct = default,
            IReadOnlyList<string> columns = null)
        {
            var tokenResult = await EnsureAccessTokenAsync(ct);
            if (tokenResult.IsFailure)
                return Result<RawTableData>.Failure(tokenResult.Error);

            var itemResult = await ResolveItemAsync(itemDescriptor, ct);
            if (itemResult.IsFailure)
                return Result<RawTableData>.Failure(itemResult.Error);

            var itemPath = itemResult.Value;

            if (columns != null && columns.Count > 0)
                return await FetchColumnsAsync(
                    itemPath, worksheetName, columns, ct);

            return await FetchUsedRangeAsync(itemPath, worksheetName, ct);
        }

        /// <summary>
        /// Fetches specific columns from a worksheet using individual range requests.
        /// Each column is fetched as "{col}:{col}" range, then merged into full-width rows.
        /// </summary>
        private async Task<Result<RawTableData>> FetchColumnsAsync(
            string itemPath, string worksheetName, IReadOnlyList<string> columns,
            CancellationToken ct)
        {
            var encodedSheet = Uri.EscapeDataString(worksheetName);

            var columnIndices = new int[columns.Count];
            int maxColIndex = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                columnIndices[i] = RawTableData.ColumnLetterToIndex(columns[i]);
                if (columnIndices[i] > maxColIndex)
                    maxColIndex = columnIndices[i];
            }
            int totalColumns = maxColIndex + 1;

            var columnData = new List<string[]>[columns.Count];
            int maxRowCount = 0;

            for (int i = 0; i < columns.Count; i++)
            {
                var colLetter = columns[i];
                var rangeAddress = $"{colLetter}:{colLetter}";
                var url = $"{GraphBaseUrl}/{itemPath}" +
                          $"/workbook/worksheets('{encodedSheet}')" +
                          $"/range(address='{rangeAddress}')" +
                          "?$select=values";

                var response = await SendGraphRequestAsync(url, ct);
                if (response.IsFailure)
                    return Result<RawTableData>.Failure(
                        $"Failed to fetch column {colLetter}: {response.Error}");

                var values = JsonLite.ParseValuesArray(response.Value);
                columnData[i] = values;
                if (values.Count > maxRowCount)
                    maxRowCount = values.Count;
            }

            if (maxRowCount == 0)
                return Result<RawTableData>.Failure("All fetched columns are empty.");

            var allRows = new string[maxRowCount][];
            for (int row = 0; row < maxRowCount; row++)
            {
                var rowData = new string[totalColumns];
                for (int c = 0; c < totalColumns; c++)
                    rowData[c] = "";

                for (int i = 0; i < columns.Count; i++)
                {
                    var colRows = columnData[i];
                    if (row < colRows.Count && colRows[row].Length > 0)
                        rowData[columnIndices[i]] = colRows[row][0];
                }

                allRows[row] = rowData;
            }

            if (maxRowCount < 2)
                return Result<RawTableData>.Failure(
                    "Fetched columns have only a header row but no data.");

            var headers = allRows[0];
            var dataRows = new string[maxRowCount - 1][];
            for (int i = 1; i < maxRowCount; i++)
                dataRows[i - 1] = allRows[i];

            return Result<RawTableData>.Success(
                new RawTableData(dataRows, headers));
        }

        /// <summary>
        /// Lists worksheet names for the editor Sheet Tab dropdown.
        /// </summary>
        internal async Task<Result<List<string>>> ListWorksheetsAsync(
            string itemDescriptor, CancellationToken ct = default)
        {
            var tokenResult = await EnsureAccessTokenAsync(ct);
            if (tokenResult.IsFailure)
                return Result<List<string>>.Failure(tokenResult.Error);

            var itemResult = await ResolveItemAsync(itemDescriptor, ct);
            if (itemResult.IsFailure)
                return Result<List<string>>.Failure(itemResult.Error);

            var url = $"{GraphBaseUrl}/{itemResult.Value}" +
                      "/workbook/worksheets?$select=name";

            var response = await SendGraphRequestAsync(url, ct);
            if (response.IsFailure)
                return Result<List<string>>.Failure(response.Error);

            try
            {
                var names = ParseWorksheetNames(response.Value);
                return Result<List<string>>.Success(names);
            }
            catch (Exception ex)
            {
                return Result<List<string>>.Failure(
                    $"Failed to parse worksheet list: {ex.Message}");
            }
        }

        private async Task<Result<RawTableData>> FetchUsedRangeAsync(
            string itemPath, string worksheetName, CancellationToken ct)
        {
            var encodedSheet = Uri.EscapeDataString(worksheetName);
            var url = $"{GraphBaseUrl}/{itemPath}" +
                      $"/workbook/worksheets('{encodedSheet}')/usedRange" +
                      "?$select=values";

            var response = await SendGraphRequestAsync(url, ct);
            if (response.IsFailure)
                return Result<RawTableData>.Failure(response.Error);

            try
            {
                var values = JsonLite.ParseValuesArray(response.Value);

                if (values == null || values.Count == 0)
                    return Result<RawTableData>.Failure(
                        $"Worksheet '{worksheetName}' is empty or has no data. " +
                        "Check that the worksheet name matches exactly " +
                        "(including capitalization) and that the sheet has content.");

                if (values.Count < 2)
                    return Result<RawTableData>.Failure(
                        "Worksheet has only a header row but no data rows.");

                var headers = values[0];
                var dataRows = new string[values.Count - 1][];
                for (var i = 1; i < values.Count; i++)
                    dataRows[i - 1] = values[i];

                return Result<RawTableData>.Success(
                    new RawTableData(dataRows, headers));
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure(
                    $"Failed to parse range data: {ex.Message}");
            }
        }

        // ==================== ITEM RESOLUTION ====================

        /// <summary>
        /// Resolves a descriptor to a Graph API item path segment.
        /// For user-delegated: "me/drive/items/{itemId}"
        /// For app-only with share links: "shares/u!{encoded}/driveItem"
        /// For app-only with item IDs: resolved through shares API first.
        /// </summary>
        private async Task<Result<string>> ResolveItemAsync(
            string descriptor, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(descriptor))
                return Result<string>.Failure("Item descriptor is empty.");

            if (descriptor.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (_isAppOnly)
                    return await ResolveShareLinkForAppOnlyAsync(descriptor, ct);
                return await ResolveShareLinkAsync(descriptor, ct);
            }

            if (descriptor.StartsWith("/"))
            {
                if (_isAppOnly)
                    return Result<string>.Failure(
                        "Path-based resolution (/path/to/file) is not supported " +
                        "with App-Only auth. Use a share link or item ID instead.");
                return await ResolveDrivePathAsync(descriptor, ct);
            }

            return Result<string>.Success($"me/drive/items/{descriptor}");
        }

        /// <summary>
        /// For user-delegated auth: resolves share link to item ID,
        /// then uses /me/drive/items/{id} path.
        /// </summary>
        private async Task<Result<string>> ResolveShareLinkAsync(
            string shareUrl, CancellationToken ct)
        {
            var encoded = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(shareUrl))
                .TrimEnd('=').Replace('/', '_').Replace('+', '-');

            var url = $"{GraphBaseUrl}/shares/u!{encoded}/driveItem?$select=id";
            var response = await SendGraphRequestAsync(url, ct);
            if (response.IsFailure)
                return Result<string>.Failure(
                    $"Failed to resolve share link: {response.Error}");

            var id = JsonLite.ExtractJsonStringField(response.Value, "id");
            return string.IsNullOrEmpty(id)
                ? Result<string>.Failure("Share link resolved but no item ID.")
                : Result<string>.Success($"me/drive/items/{id}");
        }

        /// <summary>
        /// For app-only auth: uses shares endpoint directly without /me/.
        /// The shares/{shareId}/driveItem path works with application
        /// permissions and doesn't require a user context.
        /// </summary>
        private Task<Result<string>> ResolveShareLinkForAppOnlyAsync(
            string shareUrl, CancellationToken ct)
        {
            var encoded = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(shareUrl))
                .TrimEnd('=').Replace('/', '_').Replace('+', '-');

            return Task.FromResult(
                Result<string>.Success($"shares/u!{encoded}/driveItem"));
        }

        private async Task<Result<string>> ResolveDrivePathAsync(
            string path, CancellationToken ct)
        {
            var encodedPath = Uri.EscapeDataString(path.TrimStart('/'));
            var url = $"{GraphBaseUrl}/me/drive/root:/{encodedPath}?$select=id";
            var response = await SendGraphRequestAsync(url, ct);
            if (response.IsFailure)
                return Result<string>.Failure(
                    $"Failed to resolve path '{path}': {response.Error}");

            var id = JsonLite.ExtractJsonStringField(response.Value, "id");
            return string.IsNullOrEmpty(id)
                ? Result<string>.Failure("Path resolved but no item ID.")
                : Result<string>.Success($"me/drive/items/{id}");
        }

        // ==================== HTTP ====================

        private async Task<Result<string>> SendGraphRequestAsync(
            string url, CancellationToken ct)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
            request.SetRequestHeader("Accept", "application/json");

            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }

            if (request.responseCode == 401)
                return await RefreshAndRetryAsync(url, ct);

            if (request.result != UnityWebRequest.Result.Success)
            {
                return Result<string>.Failure(
                    $"Graph API: HTTP {request.responseCode} — " +
                    request.downloadHandler.text);
            }

            return Result<string>.Success(request.downloadHandler.text);
        }

        private async Task<Result<string>> RefreshAndRetryAsync(
            string url, CancellationToken ct)
        {
            _accessToken = null;
            var tokenResult = await EnsureAccessTokenAsync(ct);
            if (tokenResult.IsFailure)
                return Result<string>.Failure(
                    $"Token refresh failed: {tokenResult.Error}");

            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
            request.SetRequestHeader("Accept", "application/json");

            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }

            return request.result != UnityWebRequest.Result.Success
                ? Result<string>.Failure(
                    $"Graph API after refresh: HTTP {request.responseCode}")
                : Result<string>.Success(request.downloadHandler.text);
        }

        private async Task<Result<bool>> EnsureAccessTokenAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(_accessToken))
                return Result<bool>.Success(true);

            if (_isAppOnly)
            {
                var clientSecret = OneDriveCredentials.ClientSecret;
                if (string.IsNullOrEmpty(clientSecret))
                    return Result<bool>.Failure(
                        "OneDrive App-Only: Client Secret is not configured. " +
                        "Set it in Project Settings > DataGraph.");

                var result = await OneDriveAppOnlyFlow.GetAccessTokenAsync(
                    _clientId, _tenantId, clientSecret, ct);
                if (result.IsFailure)
                    return Result<bool>.Failure(result.Error);

                _accessToken = result.Value;
                return Result<bool>.Success(true);
            }

            var refreshToken = OneDriveCredentials.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
                return Result<bool>.Failure(
                    "OneDrive not authenticated. " +
                    "Sign in through Project Settings > DataGraph.");

            var pkceResult = await OneDriveOAuthFlow.RefreshAccessTokenAsync(
                _clientId, _tenantId, refreshToken, ct);
            if (pkceResult.IsFailure)
                return Result<bool>.Failure(pkceResult.Error);

            _accessToken = pkceResult.Value;
            return Result<bool>.Success(true);
        }

        /// <summary>
        /// Parses worksheet names from the Graph API worksheets response.
        /// </summary>
        private static List<string> ParseWorksheetNames(string json)
        {
            var names = new List<string>();
            int pos = 0;
            while (true)
            {
                int nameIdx = json.IndexOf("\"name\"", pos, StringComparison.Ordinal);
                if (nameIdx < 0) break;

                int colonIdx = json.IndexOf(':', nameIdx + 6);
                if (colonIdx < 0) break;

                int quoteStart = json.IndexOf('"', colonIdx + 1);
                if (quoteStart < 0) break;

                var name = JsonLite.ParseJsonString(json, quoteStart, out int endPos);
                names.Add(name);
                pos = endPos;
            }
            return names;
        }
    }
}
