using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of an array field.
    /// Supports Horizontal mode (single cell, separator-split, primitives only)
    /// and Vertical mode (multi-row, index column tracked, unlimited nesting).
    /// </summary>
    internal sealed class ParseableArrayField : ParseableNode
    {
        public ParseableArrayField(
            string fieldName,
            string typeName,
            ArrayMode mode,
            string indexColumn,
            string separator,
            IReadOnlyList<ParseableNode> children)
            : base(fieldName, children)
        {
            TypeName = typeName;
            Mode = mode;
            IndexColumn = indexColumn;
            Separator = separator;
        }

        /// <summary>
        /// Base name for the generated element class (for structural children).
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// How this array reads its elements from the table.
        /// </summary>
        public ArrayMode Mode { get; }

        /// <summary>
        /// Column letter used as the index tracker in Vertical mode.
        /// Null in Horizontal mode.
        /// </summary>
        public string IndexColumn { get; }

        /// <summary>
        /// Separator character used to split values in Horizontal mode.
        /// Null in Vertical mode.
        /// </summary>
        public string Separator { get; }
    }
}
