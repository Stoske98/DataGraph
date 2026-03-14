using System.Collections.Generic;
using DataGraph.Runtime;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Mutable context carried through a parse operation.
    /// Tracks the current row position and collects warnings.
    /// </summary>
    internal sealed class ParseContext
    {
        private readonly List<ValidationEntry> _warnings = new();

        public ParseContext(RawTableData tableData)
        {
            TableData = tableData;
        }

        /// <summary>
        /// The raw table data being parsed.
        /// </summary>
        public RawTableData TableData { get; }

        /// <summary>
        /// Warnings accumulated during parsing.
        /// </summary>
        public IReadOnlyList<ValidationEntry> Warnings => _warnings;

        /// <summary>
        /// Adds a warning entry.
        /// </summary>
        public void AddWarning(string message, string nodeId = null, CellReference? cell = null)
        {
            _warnings.Add(new ValidationEntry(ValidationSeverity.Warning, message, nodeId, cell));
        }

        /// <summary>
        /// Adds an info entry.
        /// </summary>
        public void AddInfo(string message, string nodeId = null)
        {
            _warnings.Add(new ValidationEntry(ValidationSeverity.Info, message, nodeId));
        }
    }
}
