using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using DataGraph.GoogleSheets.Auth;
using DataGraph.Runtime;

using UnityEngine;
using UnityEngine.Networking;

namespace DataGraph.GoogleSheets.Fetch
{
    /// <summary>
    /// Fetches raw tabular data from Google Sheets API v4.
    /// Supports both full range and batch column fetching.
    /// </summary>
    internal sealed class GoogleSheetsFetcher
    {
        private const string ApiBaseUrl = "https://sheets.googleapis.com/v4/spreadsheets";

        /// <summary>
        /// Fetches data from the specified sheet using the given credentials.
        /// If columns is provided, uses batchGet to fetch only those columns.
        /// </summary>
        public async Task<Result<RawTableData>> FetchAsync(
            string spreadsheetId,
            AuthCredentials credentials,
            SheetReference reference,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string> columns = null)
        {
            if (columns != null && columns.Count > 0)
                return await FetchBatchAsync(
                    spreadsheetId, credentials, reference, columns, cancellationToken);

            return await FetchFullRangeAsync(
                spreadsheetId, credentials, reference, cancellationToken);
        }

        private async Task<Result<RawTableData>> FetchFullRangeAsync(
            string spreadsheetId,
            AuthCredentials credentials,
            SheetReference reference,
            CancellationToken cancellationToken)
        {
            var range = string.IsNullOrEmpty(reference.Range)
                ? "Sheet1"
                : reference.Range;

            var url = $"{ApiBaseUrl}/{Uri.EscapeDataString(spreadsheetId)}" +
                      $"/values/{Uri.EscapeDataString(range)}" +
                      $"?valueRenderOption=FORMATTED_VALUE" +
                      $"&dateTimeRenderOption=FORMATTED_STRING";

            if (credentials.IsApiKey)
                url += $"&key={Uri.EscapeDataString(credentials.ApiKey)}";

            try
            {
                using var request = UnityWebRequest.Get(url);

                if (!credentials.IsApiKey)
                    request.SetRequestHeader("Authorization",
                        $"Bearer {credentials.BearerToken}");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                    return MapHttpError(request);

                return ParseResponse(request.downloadHandler.text, reference.HeaderRowOffset);
            }
            catch (OperationCanceledException)
            {
                return Result<RawTableData>.Failure("Fetch was cancelled.");
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure($"Fetch failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetches only specified columns using batchGet API.
        /// Each column becomes a separate range (e.g., "Sheet1!A:A").
        /// Results are merged into full-width rows with empty strings
        /// for non-fetched columns.
        /// </summary>
        private async Task<Result<RawTableData>> FetchBatchAsync(
            string spreadsheetId,
            AuthCredentials credentials,
            SheetReference reference,
            IReadOnlyList<string> columns,
            CancellationToken cancellationToken)
        {
            var sheetName = string.IsNullOrEmpty(reference.Range)
                ? "Sheet1"
                : reference.Range;

            var rangeParams = new System.Text.StringBuilder();
            foreach (var col in columns)
            {
                if (rangeParams.Length > 0) rangeParams.Append('&');
                var range = $"{sheetName}!{col}:{col}";
                rangeParams.Append($"ranges={Uri.EscapeDataString(range)}");
            }

            var url = $"{ApiBaseUrl}/{Uri.EscapeDataString(spreadsheetId)}" +
                      $"/values:batchGet?{rangeParams}" +
                      $"&valueRenderOption=FORMATTED_VALUE" +
                      $"&dateTimeRenderOption=FORMATTED_STRING";

            if (credentials.IsApiKey)
                url += $"&key={Uri.EscapeDataString(credentials.ApiKey)}";

            try
            {
                using var request = UnityWebRequest.Get(url);

                if (!credentials.IsApiKey)
                    request.SetRequestHeader("Authorization",
                        $"Bearer {credentials.BearerToken}");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                    return MapHttpError(request);

                return ParseBatchResponse(
                    request.downloadHandler.text, columns, reference.HeaderRowOffset);
            }
            catch (OperationCanceledException)
            {
                return Result<RawTableData>.Failure("Fetch was cancelled.");
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure($"Batch fetch failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses batchGet response. Each valueRange has a single column of data.
        /// Merges them into full-width rows where non-fetched columns are empty strings.
        /// </summary>
        private static Result<RawTableData> ParseBatchResponse(
            string json, IReadOnlyList<string> columns, int headerRowOffset)
        {
            try
            {
                var valueRanges = ParseBatchValueRanges(json);

                if (valueRanges.Count == 0)
                    return Result<RawTableData>.Failure("Batch response has no data.");

                if (valueRanges.Count != columns.Count)
                    return Result<RawTableData>.Failure(
                        $"Expected {columns.Count} value ranges but got {valueRanges.Count}.");

                // Find max column index and max row count
                int maxColIndex = 0;
                int maxRowCount = 0;
                var columnIndices = new int[columns.Count];
                for (int i = 0; i < columns.Count; i++)
                {
                    columnIndices[i] = RawTableData.ColumnLetterToIndex(columns[i]);
                    if (columnIndices[i] > maxColIndex)
                        maxColIndex = columnIndices[i];
                    if (valueRanges[i].Count > maxRowCount)
                        maxRowCount = valueRanges[i].Count;
                }

                int totalColumns = maxColIndex + 1;

                if (maxRowCount == 0)
                    return Result<RawTableData>.Failure("All fetched columns are empty.");

                // Build full-width rows
                var allRows = new string[maxRowCount][];
                for (int row = 0; row < maxRowCount; row++)
                {
                    var rowData = new string[totalColumns];
                    for (int c = 0; c < totalColumns; c++)
                        rowData[c] = "";

                    for (int i = 0; i < columns.Count; i++)
                    {
                        var colData = valueRanges[i];
                        if (row < colData.Count && colData[row].Length > 0)
                            rowData[columnIndices[i]] = colData[row][0];
                    }

                    allRows[row] = rowData;
                }

                // Split header and data rows
                if (maxRowCount <= headerRowOffset)
                    return Result<RawTableData>.Failure(
                        $"Only {maxRowCount} rows but header offset is {headerRowOffset}.");

                var headerRow = headerRowOffset > 0
                    ? allRows[headerRowOffset - 1]
                    : GenerateDefaultHeaders(totalColumns);

                int dataStartRow = headerRowOffset;
                int dataRowCount = maxRowCount - dataStartRow;
                var dataRows = new string[dataRowCount][];
                for (int i = 0; i < dataRowCount; i++)
                    dataRows[i] = allRows[dataStartRow + i];

                return Result<RawTableData>.Success(
                    new RawTableData(dataRows, headerRow));
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure(
                    $"Failed to parse batch response: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses the "valueRanges" array from batchGet JSON response.
        /// Each element has a "values" field which is a 2D array.
        /// For single-column ranges, each inner array has exactly one element.
        /// </summary>
        private static List<List<string[]>> ParseBatchValueRanges(string json)
        {
            var result = new List<List<string[]>>();

            int vrIndex = json.IndexOf("\"valueRanges\"", StringComparison.Ordinal);
            if (vrIndex < 0) return result;

            int outerStart = json.IndexOf('[', vrIndex);
            if (outerStart < 0) return result;

            // Find each "values" within valueRanges
            int pos = outerStart + 1;
            while (pos < json.Length)
            {
                int valuesIdx = json.IndexOf("\"values\"", pos, StringComparison.Ordinal);
                if (valuesIdx < 0) break;

                // Check we haven't gone past the end of valueRanges array
                int closingBracket = json.IndexOf(']', outerStart + 1);
                // Find matching closing bracket at depth 1
                int depth = 1;
                int scanPos = outerStart + 1;
                while (scanPos < json.Length && depth > 0)
                {
                    if (json[scanPos] == '[') depth++;
                    else if (json[scanPos] == ']') depth--;
                    if (depth > 0) scanPos++;
                }
                if (valuesIdx > scanPos) break;

                var values = JsonLite.ParseValuesArray(json, valuesIdx);
                result.Add(values);

                pos = valuesIdx + 10;
            }

            return result;
        }

        // ==================== SINGLE RANGE PARSING ====================

        private static Result<RawTableData> ParseResponse(string json, int headerRowOffset)
        {
            try
            {
                int valuesIndex = json.IndexOf("\"values\"", StringComparison.Ordinal);
                var values = valuesIndex >= 0
                    ? JsonLite.ParseValuesArray(json, valuesIndex)
                    : new List<string[]>();

                if (values.Count == 0)
                    return Result<RawTableData>.Failure("Sheet returned no data.");

                if (values.Count <= headerRowOffset)
                    return Result<RawTableData>.Failure(
                        $"Sheet has {values.Count} rows but header offset " +
                        $"is {headerRowOffset}. No data rows available.");

                var headerRow = headerRowOffset > 0 && headerRowOffset <= values.Count
                    ? values[headerRowOffset - 1]
                    : GenerateDefaultHeaders(
                        values.Count > 0 ? values[0].Length : 0);

                int dataStartRow = headerRowOffset;
                int dataRowCount = values.Count - dataStartRow;
                var dataRows = new string[dataRowCount][];

                for (int i = 0; i < dataRowCount; i++)
                    dataRows[i] = values[dataStartRow + i];

                return Result<RawTableData>.Success(
                    new RawTableData(dataRows, headerRow));
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure(
                    $"Failed to parse API response: {ex.Message}");
            }
        }

        private static Result<RawTableData> MapHttpError(UnityWebRequest request)
        {
            return request.responseCode switch
            {
                401 => Result<RawTableData>.Failure(
                    "Authentication expired. Please re-authenticate."),
                403 => Result<RawTableData>.Failure(
                    "Access denied. Check sheet sharing permissions and API key restrictions."),
                404 => Result<RawTableData>.Failure(
                    "Spreadsheet not found. Check the URL or ID."),
                _ => Result<RawTableData>.Failure(
                    $"Google Sheets API error ({request.responseCode}): {request.error}")
            };
        }

        private static string[] GenerateDefaultHeaders(int count)
        {
            var headers = new string[count];
            for (int i = 0; i < count; i++)
                headers[i] = RawTableData.IndexToColumnLetter(i);
            return headers;
        }
    }
}
