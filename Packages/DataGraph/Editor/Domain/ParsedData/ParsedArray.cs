using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// A parsed ordered list of elements.
    /// Elements can be ParsedValue (for primitive arrays)
    /// or ParsedObject (for structural arrays).
    /// </summary>
    internal sealed class ParsedArray : ParsedNode
    {
        public ParsedArray(
            string fieldName,
            string elementTypeName,
            IReadOnlyList<ParsedNode> elements)
            : base(fieldName)
        {
            ElementTypeName = elementTypeName;
            Elements = elements;
        }

        /// <summary>
        /// Type name of the array elements (for code generation).
        /// </summary>
        public string ElementTypeName { get; }

        /// <summary>
        /// The parsed elements in order.
        /// </summary>
        public IReadOnlyList<ParsedNode> Elements { get; }
    }
}
