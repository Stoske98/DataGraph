using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataGraph.LocalFile
{
    /// <summary>
    /// Parses CSV files into a jagged string array.
    /// Handles quoted fields, embedded commas, newlines within quotes,
    /// and escaped double-quotes (RFC 4180 compliant).
    /// </summary>
    internal static class CsvParser
    {
        /// <summary>
        /// Parses CSV content from a file path.
        /// </summary>
        public static string[][] Parse(string filePath, char delimiter = ',')
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            return ParseContent(content, delimiter);
        }

        /// <summary>
        /// Parses CSV content from a string.
        /// </summary>
        public static string[][] ParseContent(string content, char delimiter = ',')
        {
            var rows = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int i = 0;

            while (i < content.Length)
            {
                char c = content[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                        }
                        else
                        {
                            inQuotes = false;
                            i++;
                        }
                    }
                    else
                    {
                        field.Append(c);
                        i++;
                    }
                }
                else
                {
                    if (c == '"' && field.Length == 0)
                    {
                        inQuotes = true;
                        i++;
                    }
                    else if (c == delimiter)
                    {
                        fields.Add(field.ToString());
                        field.Clear();
                        i++;
                    }
                    else if (c == '\r' || c == '\n')
                    {
                        fields.Add(field.ToString());
                        field.Clear();

                        if (fields.Count > 0 && !(fields.Count == 1 && string.IsNullOrEmpty(fields[0])))
                            rows.Add(fields.ToArray());
                        fields.Clear();

                        if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                            i += 2;
                        else
                            i++;
                    }
                    else
                    {
                        field.Append(c);
                        i++;
                    }
                }
            }

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                if (fields.Count > 0 && !(fields.Count == 1 && string.IsNullOrEmpty(fields[0])))
                    rows.Add(fields.ToArray());
            }

            return rows.ToArray();
        }
    }
}
