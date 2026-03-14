namespace DataGraph.Runtime
{
    /// <summary>
    /// Identifies a specific cell in the source table
    /// by row index and column identifier.
    /// </summary>
    public readonly struct CellReference
    {
        public CellReference(int row, string column)
        {
            Row = row;
            Column = column;
        }

        /// <summary>
        /// Zero-based row index in the table.
        /// </summary>
        public int Row { get; }

        /// <summary>
        /// Column identifier (e.g. "A", "B", "C").
        /// </summary>
        public string Column { get; }

        public override string ToString() => $"{Column}{Row}";
    }
}
