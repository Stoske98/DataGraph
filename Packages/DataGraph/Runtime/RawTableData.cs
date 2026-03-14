using System;
using System.Collections.Generic;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Immutable representation of raw tabular data fetched from a sheet.
    /// All values are strings — type coercion happens during parsing.
    /// </summary>
    public sealed class RawTableData
    {
        private readonly string[][] _rows;

        public RawTableData(string[][] rows, string[] headers)
        {
            _rows = rows ?? throw new ArgumentNullException(nameof(rows));
            Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        /// <summary>
        /// Column headers extracted from the header row.
        /// </summary>
        public IReadOnlyList<string> Headers { get; }

        /// <summary>
        /// Number of data rows (excluding header).
        /// </summary>
        public int RowCount => _rows.Length;

        /// <summary>
        /// Number of columns based on the header row.
        /// </summary>
        public int ColumnCount => Headers.Count;

        /// <summary>
        /// Gets the raw string value at the given row and column index.
        /// Returns empty string if the cell is out of bounds or null.
        /// </summary>
        public string GetCell(int row, int column)
        {
            if (row < 0 || row >= _rows.Length)
                return string.Empty;
            var rowData = _rows[row];
            if (column < 0 || column >= rowData.Length)
                return string.Empty;
            return rowData[column] ?? string.Empty;
        }

        /// <summary>
        /// Gets the raw string value at the given row using a column letter (A, B, C, ...).
        /// </summary>
        public string GetCell(int row, string columnLetter)
        {
            int colIndex = ColumnLetterToIndex(columnLetter);
            return GetCell(row, colIndex);
        }

        /// <summary>
        /// Returns the full row as a read-only list of strings.
        /// Returns an empty array if the row is out of bounds.
        /// </summary>
        public IReadOnlyList<string> GetRow(int row)
        {
            if (row < 0 || row >= _rows.Length)
                return Array.Empty<string>();
            return _rows[row];
        }

        /// <summary>
        /// Converts a column letter (A, B, ..., Z, AA, AB, ...) to a zero-based index.
        /// </summary>
        public static int ColumnLetterToIndex(string letter)
        {
            if (string.IsNullOrEmpty(letter))
                return -1;

            int index = 0;
            for (int i = 0; i < letter.Length; i++)
            {
                char c = char.ToUpperInvariant(letter[i]);
                if (c < 'A' || c > 'Z')
                    return -1;
                index = index * 26 + (c - 'A' + 1);
            }
            return index - 1;
        }

        /// <summary>
        /// Converts a zero-based column index to a letter (0=A, 1=B, ..., 25=Z, 26=AA).
        /// </summary>
        public static string IndexToColumnLetter(int index)
        {
            if (index < 0)
                return string.Empty;

            var result = string.Empty;
            while (index >= 0)
            {
                result = (char)('A' + index % 26) + result;
                index = index / 26 - 1;
            }
            return result;
        }
    }
}
