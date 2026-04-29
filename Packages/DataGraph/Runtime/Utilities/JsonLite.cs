using System;
using System.Collections.Generic;
using System.Text;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Hand-rolled JSON parsing helpers for fetcher payloads.
    /// Used because Unity's JsonUtility cannot deserialize jagged arrays
    /// (string[][]) like the "values" payload returned by Google Sheets and
    /// Microsoft Graph. Optimized for the specific shape of those responses,
    /// not a general-purpose JSON parser.
    /// </summary>
    internal static class JsonLite
    {
        /// <summary>
        /// Parses a "values" 2D array starting from the given key position.
        /// The position must lie at or before the "values"/"Values" key in
        /// <paramref name="json"/>; the parser scans forward to the first
        /// '[' and reads inner string arrays until the matching closing ']'.
        /// </summary>
        public static List<string[]> ParseValuesArray(string json, int valuesKeyPos)
        {
            var result = new List<string[]>();

            int outerArrayStart = json.IndexOf('[', valuesKeyPos);
            if (outerArrayStart < 0) return result;

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
        /// Convenience overload that locates the "values" (or capitalized
        /// "Values") key in the JSON and parses the 2D array from there.
        /// Returns an empty list if neither key is found.
        /// </summary>
        public static List<string[]> ParseValuesArray(string json)
        {
            int idx = json.IndexOf("\"values\"", StringComparison.Ordinal);
            if (idx < 0)
            {
                idx = json.IndexOf("\"Values\"", StringComparison.Ordinal);
                if (idx < 0) return new List<string[]>();
            }
            return ParseValuesArray(json, idx);
        }

        /// <summary>
        /// Parses a one-dimensional array of strings starting at the opening
        /// '[' position. On return <paramref name="endPos"/> points one past
        /// the closing ']'. Unquoted tokens are accepted (numbers, booleans,
        /// the bare literal "null" maps to empty string).
        /// </summary>
        public static string[] ParseStringArray(string json, int startPos, out int endPos)
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

        /// <summary>
        /// Parses a quoted JSON string starting at the opening '"' position.
        /// Handles standard escape sequences (\", \\, \n, \r, \t, \/).
        /// On return <paramref name="endPos"/> points one past the closing '"'.
        /// </summary>
        public static string ParseJsonString(string json, int startPos, out int endPos)
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

        /// <summary>
        /// Reads an unquoted token (number, true/false/null) until the next
        /// ',' ']' or '}'. The bare literal "null" is normalized to an empty
        /// string to match column-cell semantics in fetcher payloads.
        /// </summary>
        public static string ParseUnquotedValue(string json, int startPos, out int endPos)
        {
            int pos = startPos;
            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == ',' || c == ']' || c == '}') break;
                pos++;
            }
            endPos = pos;
            var value = json.Substring(startPos, pos - startPos).Trim();
            return value == "null" ? "" : value;
        }

        /// <summary>
        /// Extracts a single string field by name from a flat JSON object
        /// without full parsing. Returns null if the field is not present.
        /// Used for one-shot lookups like driveItem { "id": "..." }.
        /// </summary>
        public static string ExtractJsonStringField(string json, string fieldName)
        {
            var key = "\"" + fieldName + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;

            int colonIdx = json.IndexOf(':', idx + key.Length);
            if (colonIdx < 0) return null;

            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0) return null;

            return ParseJsonString(json, quoteStart, out _);
        }
    }
}
