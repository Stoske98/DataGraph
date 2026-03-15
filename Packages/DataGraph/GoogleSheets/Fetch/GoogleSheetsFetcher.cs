using System;
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
    /// Supports both API Key (query param) and Bearer token (header) auth.
    /// </summary>
    internal sealed class GoogleSheetsFetcher
    {
        private const string ApiBaseUrl = "https://sheets.googleapis.com/v4/spreadsheets";

        /// <summary>
        /// Fetches data from the specified sheet using the given credentials.
        /// </summary>
        public async Task<Result<RawTableData>> FetchAsync(
            string spreadsheetId,
            AuthCredentials credentials,
            SheetReference reference,
            CancellationToken cancellationToken = default)
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

        private static Result<RawTableData> ParseResponse(string json, int headerRowOffset)
        {
            try
            {
                var response = JsonUtility.FromJson<SheetsValuesResponse>(json);

                if (response?.values == null || response.values.Length == 0)
                    return Result<RawTableData>.Failure("Sheet returned no data.");

                if (response.values.Length <= headerRowOffset)
                    return Result<RawTableData>.Failure(
                        $"Sheet has {response.values.Length} rows but header offset " +
                        $"is {headerRowOffset}. No data rows available.");

                var headerRow = headerRowOffset > 0 && headerRowOffset <= response.values.Length
                    ? response.values[headerRowOffset - 1]
                    : GenerateDefaultHeaders(
                        response.values.Length > 0 ? response.values[0].Length : 0);

                int dataStartRow = headerRowOffset;
                int dataRowCount = response.values.Length - dataStartRow;
                var dataRows = new string[dataRowCount][];

                for (int i = 0; i < dataRowCount; i++)
                    dataRows[i] = response.values[dataStartRow + i];

                return Result<RawTableData>.Success(
                    new RawTableData(dataRows, headerRow));
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure(
                    $"Failed to parse API response: {ex.Message}");
            }
        }

        private static string[] GenerateDefaultHeaders(int count)
        {
            var headers = new string[count];
            for (int i = 0; i < count; i++)
                headers[i] = RawTableData.IndexToColumnLetter(i);
            return headers;
        }

        [Serializable]
        private class SheetsValuesResponse
        {
            public string range;
            public string majorDimension;
            public string[][] values;
        }
    }
}
