using System.Collections.Generic;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Identifies a specific spreadsheet and range to fetch data from.
    /// Provider-agnostic — works with Google Sheets, OneDrive, CSV, etc.
    /// </summary>
    public sealed class SheetReference
    {
        public SheetReference(
            string sheetId,
            int headerRowOffset = 1,
            string range = null,
            IReadOnlyList<string> columns = null)
        {
            SheetId = sheetId;
            HeaderRowOffset = headerRowOffset;
            Range = range;
            Columns = columns;
        }

        /// <summary>
        /// Provider-specific sheet identifier (URL, file path, or ID).
        /// </summary>
        public string SheetId { get; }

        /// <summary>
        /// Number of rows to skip before the first data row. Default is 1
        /// (assumes first row is the header).
        /// </summary>
        public int HeaderRowOffset { get; }

        /// <summary>
        /// Optional range restriction (e.g. "A1:G100") or sheet tab name.
        /// Null means entire sheet.
        /// </summary>
        public string Range { get; }

        /// <summary>
        /// Optional list of column letters to fetch (e.g. ["A", "C", "F"]).
        /// When set, providers that support batch fetching will request
        /// only these columns instead of the full sheet.
        /// Null means fetch all columns.
        /// </summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>
        /// Whether selective column fetching is requested.
        /// </summary>
        public bool HasColumnFilter => Columns != null && Columns.Count > 0;
    }
}
