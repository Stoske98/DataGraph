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
                var values = ParseValuesArray(json);

                if (values == null || values.Count == 0)
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

        /// <summary>
        /// Manually parses the "values" field from Google Sheets API JSON response.
        /// JsonUtility cannot handle string[][] so we parse it ourselves.
        /// </summary>
        private static System.Collections.Generic.List<string[]> ParseValuesArray(string json)
        {
            var result = new System.Collections.Generic.List<string[]>();

            int valuesIndex = json.IndexOf("\"values\"", StringComparison.Ordinal);
            if (valuesIndex < 0)
                return result;

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

        /// <summary>
        /// Parses a single JSON string array starting at the '[' character.
        /// Returns the array and sets endPos to the position after ']'.
        /// </summary>
        private static string[] ParseStringArray(string json, int startPos, out int endPos)
        {
            var items = new System.Collections.Generic.List<string>();
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

                pos++;
            }

            endPos = pos;
            return items.ToArray();
        }

        /// <summary>
        /// Parses a JSON quoted string starting at the opening '"'.
        /// Handles escape sequences.
        /// </summary>
        private static string ParseJsonString(string json, int startPos, out int endPos)
        {
            var sb = new System.Text.StringBuilder();
            int pos = startPos + 1;

            while (pos < json.Length)
            {
                char c = json[pos];

                if (c == '\\' && pos + 1 < json.Length)
                {
                    char next = json[pos + 1];
                    switch (next)
                    {
                        case '"':
                            sb.Append('"');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        default:
                            sb.Append(next);
                            break;
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

        private static string[] GenerateDefaultHeaders(int count)
        {
            var headers = new string[count];
            for (int i = 0; i < count; i++)
                headers[i] = RawTableData.IndexToColumnLetter(i);
            return headers;
        }

    }
}
