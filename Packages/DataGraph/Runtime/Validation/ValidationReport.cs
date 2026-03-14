using System.Collections.Generic;
using System.Linq;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Aggregates all validation entries from graph validation
    /// or data parsing into a single queryable report.
    /// </summary>
    public sealed class ValidationReport
    {
        public static readonly ValidationReport Empty = new(new List<ValidationEntry>());

        public ValidationReport(IReadOnlyList<ValidationEntry> entries)
        {
            Entries = entries;
        }

        /// <summary>
        /// All validation entries in this report.
        /// </summary>
        public IReadOnlyList<ValidationEntry> Entries { get; }

        /// <summary>
        /// True if any entry has Error severity.
        /// </summary>
        public bool HasErrors => Entries.Any(e => e.Severity == ValidationSeverity.Error);

        /// <summary>
        /// True if any entry has Warning severity.
        /// </summary>
        public bool HasWarnings => Entries.Any(e => e.Severity == ValidationSeverity.Warning);

        /// <summary>
        /// True if no entries have Error severity.
        /// </summary>
        public bool IsValid => !HasErrors;

        /// <summary>
        /// Returns only entries with the given severity.
        /// </summary>
        public IEnumerable<ValidationEntry> GetBySeverity(ValidationSeverity severity) =>
            Entries.Where(e => e.Severity == severity);

        /// <summary>
        /// Returns only entries associated with the given node.
        /// </summary>
        public IEnumerable<ValidationEntry> GetByNode(string nodeId) =>
            Entries.Where(e => e.SourceNodeId == nodeId);

        /// <summary>
        /// Creates a new report by combining this report with another.
        /// </summary>
        public ValidationReport Merge(ValidationReport other)
        {
            var combined = new List<ValidationEntry>(Entries.Count + other.Entries.Count);
            combined.AddRange(Entries);
            combined.AddRange(other.Entries);
            return new ValidationReport(combined);
        }
    }
}
