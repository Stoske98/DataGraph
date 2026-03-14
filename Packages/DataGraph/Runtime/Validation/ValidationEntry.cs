namespace DataGraph.Runtime
{
    /// <summary>
    /// A single validation finding produced during graph validation
    /// or data parsing. Carries severity, a human-readable message,
    /// and optional source location identifiers.
    /// </summary>
    public sealed class ValidationEntry
    {
        public ValidationEntry(
            ValidationSeverity severity,
            string message,
            string sourceNodeId = null,
            CellReference? sourceCell = null)
        {
            Severity = severity;
            Message = message;
            SourceNodeId = sourceNodeId;
            SourceCell = sourceCell;
        }

        /// <summary>
        /// How severe this finding is.
        /// </summary>
        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Human-readable description of the issue.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Identifier of the graph node that caused this entry, if applicable.
        /// </summary>
        public string SourceNodeId { get; }

        /// <summary>
        /// Cell coordinates in the source table, if applicable.
        /// </summary>
        public CellReference? SourceCell { get; }

        public override string ToString() => $"[{Severity}] {Message}";
    }
}
