namespace DataGraph.Runtime
{
    /// <summary>
    /// Identifies a specific spreadsheet and range to fetch data from.
    /// Provider-agnostic — works with Google Sheets, Excel, CSV, etc.
    /// </summary>
    public sealed class SheetReference
    {
        public SheetReference(
            string sheetId,
            int headerRowOffset = 1,
            string range = null)
        {
            SheetId = sheetId;
            HeaderRowOffset = headerRowOffset;
            Range = range;
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
        /// Optional range restriction (e.g. "A1:G100"). Null means entire sheet.
        /// </summary>
        public string Range { get; }
    }
}
