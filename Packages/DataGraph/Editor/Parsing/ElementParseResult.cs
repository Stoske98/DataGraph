using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Result of parsing a single element from the table.
    /// Carries the parsed node and the number of rows consumed,
    /// which is critical for vertical array depth propagation.
    /// </summary>
    internal readonly struct ElementParseResult
    {
        public ElementParseResult(ParsedNode node, int depth)
        {
            Node = node;
            Depth = depth;
        }

        /// <summary>
        /// The parsed data node.
        /// </summary>
        public ParsedNode Node { get; }

        /// <summary>
        /// Number of table rows consumed by this element.
        /// Always 1 for flat fields; may be greater for elements
        /// containing vertical arrays.
        /// </summary>
        public int Depth { get; }
    }
}
