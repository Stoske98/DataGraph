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
    /// </summary>
    internal sealed class OneDriveFetcher
    {
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

        private readonly string _clientId;
        private readonly string _tenantId;
        private string _accessToken;

        internal OneDriveFetcher(string clientId, string tenantId)
        {
            _clientId = clientId;
            _tenantId = tenantId;
        }

        /// <summary>
        /// Fetches worksheet data from an Excel file on OneDrive.
        /// </summary>
        internal async Task<Result<RawTableData>> FetchAsync(
            string itemDescriptor, string worksheetName,
            CancellationToken ct = default)
        {
            var tokenResult = await EnsureAccessTokenAsync(ct);
            if (tokenResult.IsFailure)
                return Result<RawTableData>.Failure(tokenResult.Error);

            var itemIdResult = await ResolveItemIdAsync(itemDescriptor, ct);
            if (itemIdResult.IsFailure)
                return Result<RawTableData>.Failure(itemIdResult.Error);

            return await FetchUsedRangeAsync(itemIdResult.Value, worksheetName, ct);
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

            var itemIdResult = await ResolveItemIdAsync(itemDescriptor, ct);
            if (itemIdResult.IsFailure)
                return Result<List<string>>.Failure(itemIdResult.Error);

            var url = $"{GraphBaseUrl}/me/drive/items/{itemIdResult.Value}" +
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
            string itemId, string worksheetName, CancellationToken ct)
        {
            var encodedSheet = Uri.EscapeDataString(worksheetName);
            var url = $"{GraphBaseUrl}/me/drive/items/{itemId}" +
                      $"/workbook/worksheets('{encodedSheet}')/usedRange" +
                      "?$select=values";

            var response = await SendGraphRequestAsync(url, ct);
            if (response.IsFailure)
                return Result<RawTableData>.Failure(response.Error);

            try
            {
                var values = ParseValuesArray(response.Value);

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

        private async Task<Result<string>> ResolveItemIdAsync(
            string descriptor, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(descriptor))
                return Result<string>.Failure("Item descriptor is empty.");
            if (descriptor.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return await ResolveShareLinkAsync(descriptor, ct);
            if (descriptor.StartsWith("/"))
                return await ResolveDrivePathAsync(descriptor, ct);
            return Result<string>.Success(descriptor);
        }

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

            var id = ExtractJsonStringField(response.Value, "id");
            return string.IsNullOrEmpty(id)
                ? Result<string>.Failure("Share link resolved but no item ID.")
                : Result<string>.Success(id);
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

            var id = ExtractJsonStringField(response.Value, "id");
            return string.IsNullOrEmpty(id)
                ? Result<string>.Failure("Path resolved but no item ID.")
                : Result<string>.Success(id);
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
            var refreshResult = await OneDriveOAuthFlow.RefreshAccessTokenAsync(
                _clientId, _tenantId, OneDriveCredentials.RefreshToken, ct);
            if (refreshResult.IsFailure)
                return Result<string>.Failure(
                    $"Token refresh failed: {refreshResult.Error}");

            _accessToken = refreshResult.Value;

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

            var refreshToken = OneDriveCredentials.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
                return Result<bool>.Failure(
                    "OneDrive not authenticated. " +
                    "Sign in through Project Settings > DataGraph.");

            var result = await OneDriveOAuthFlow.RefreshAccessTokenAsync(
                _clientId, _tenantId, refreshToken, ct);
            if (result.IsFailure)
                return Result<bool>.Failure(result.Error);

            _accessToken = result.Value;
            return Result<bool>.Success(true);
        }

        // ==================== JSON PARSING ====================
        // Unity's JsonUtility cannot deserialize string[][] (jagged arrays).
        // Manual parsing approach identical to GoogleSheetsFetcher.

        /// <summary>
        /// Parses the "values" field from Microsoft Graph usedRange response.
        /// Response format: { "values": [["h1","h2"],["v1","v2"]] }
        /// </summary>
        private static List<string[]> ParseValuesArray(string json)
        {
            var result = new List<string[]>();

            int valuesIndex = json.IndexOf("\"values\"", StringComparison.Ordinal);
            if (valuesIndex < 0)
            {
                valuesIndex = json.IndexOf("\"Values\"", StringComparison.Ordinal);
                if (valuesIndex < 0)
                    return result;
            }

            int outerArrayStart = json.IndexOf('[', valuesIndex);
            if (outerArrayStart < 0)
                return result;

            int pos = outerArrayStart + 1;
            int depth = 1;

            while (pos < json.Length && depth > 0)
            {
                char c = json[pos];

                if (c == '[')
                {
                    if (depth == 1)
                    {
                        var row = ParseStringArray(json, pos, out int endPos);
                        result.Add(row);
                        pos = endPos;
                        continue;
                    }
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }

                pos++;
            }

            return result;
        }

        private static string[] ParseStringArray(string json, int startPos, out int endPos)
        {
            var items = new List<string>();
            int pos = startPos + 1;

            while (pos < json.Length)
            {
                char c = json[pos];

                if (c == ']')
                {
                    endPos = pos + 1;
                    return items.ToArray();
                }

                if (c == '"')
                {
                    var value = ParseJsonString(json, pos, out int strEnd);
                    items.Add(value);
                    pos = strEnd;
                    continue;
                }

                // Handle unquoted values (numbers, booleans, null)
                if (c != ',' && c != ' ' && c != '\n' && c != '\r' && c != '\t')
                {
                    var value = ParseUnquotedValue(json, pos, out int valEnd);
                    items.Add(value);
                    pos = valEnd;
                    continue;
                }

                pos++;
            }

            endPos = pos;
            return items.ToArray();
        }

        private static string ParseJsonString(string json, int startPos, out int endPos)
        {
            var sb = new StringBuilder();
            int pos = startPos + 1;

            while (pos < json.Length)
            {
                char c = json[pos];

                if (c == '\\' && pos + 1 < json.Length)
                {
                    char next = json[pos + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '/': sb.Append('/'); break;
                        default: sb.Append(next); break;
                    }
                    pos += 2;
                    continue;
                }

                if (c == '"')
                {
                    endPos = pos + 1;
                    return sb.ToString();
                }

                sb.Append(c);
                pos++;
            }

            endPos = pos;
            return sb.ToString();
        }

        private static string ParseUnquotedValue(string json, int startPos, out int endPos)
        {
            int pos = startPos;
            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == ',' || c == ']' || c == '}')
                    break;
                pos++;
            }
            endPos = pos;
            var value = json.Substring(startPos, pos - startPos).Trim();
            return value == "null" ? "" : value;
        }

        /// <summary>
        /// Extracts a simple string field value from JSON without full parsing.
        /// Used for single-field responses like driveItem { "id": "..." }.
        /// </summary>
        private static string ExtractJsonStringField(string json, string fieldName)
        {
            var key = $"\"{fieldName}\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;

            int colonIdx = json.IndexOf(':', idx + key.Length);
            if (colonIdx < 0) return null;

            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0) return null;

            var value = ParseJsonString(json, quoteStart, out _);
            return value;
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

                var name = ParseJsonString(json, quoteStart, out int endPos);
                names.Add(name);
                pos = endPos;
            }
            return names;
        }
    }
}
