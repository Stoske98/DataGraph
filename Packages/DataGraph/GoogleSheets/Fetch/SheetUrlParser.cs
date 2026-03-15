using System.Text.RegularExpressions;
using DataGraph.Runtime;

namespace DataGraph.GoogleSheets.Fetch
{
    /// <summary>
    /// Extracts the spreadsheet ID from various Google Sheets URL formats.
    /// </summary>
    internal static class SheetUrlParser
    {
        private static readonly Regex SpreadsheetIdRegex = new(
            @"/spreadsheets/d/([a-zA-Z0-9_-]+)",
            RegexOptions.Compiled);

        private static readonly Regex RawIdRegex = new(
            @"^[a-zA-Z0-9_-]{20,}$",
            RegexOptions.Compiled);

        public static Result<string> ExtractSpreadsheetId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Result<string>.Failure("Sheet ID or URL is empty.");

            var trimmed = input.Trim();

            var urlMatch = SpreadsheetIdRegex.Match(trimmed);
            if (urlMatch.Success)
                return Result<string>.Success(urlMatch.Groups[1].Value);

            if (RawIdRegex.IsMatch(trimmed))
                return Result<string>.Success(trimmed);

            return Result<string>.Failure(
                $"Cannot extract spreadsheet ID from '{input}'.");
        }
    }
}
