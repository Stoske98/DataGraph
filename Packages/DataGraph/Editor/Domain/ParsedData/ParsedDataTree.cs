using System.Collections.Generic;
using DataGraph.Runtime;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Immutable intermediate representation of parsed data.
    /// Format-agnostic — the Serializer layer is responsible for
    /// format-specific transformations. This is the boundary
    /// between parsing and output generation.
    /// </summary>
    internal sealed class ParsedDataTree
    {
        public ParsedDataTree(
            ParsedNode root,
            ParseableGraph sourceGraph,
            IReadOnlyList<ValidationEntry> parseWarnings)
        {
            Root = root;
            SourceGraph = sourceGraph;
            ParseWarnings = parseWarnings ?? System.Array.Empty<ValidationEntry>();
        }

        /// <summary>
        /// The root parsed data node (dictionary, array, or single object).
        /// </summary>
        public ParsedNode Root { get; }

        /// <summary>
        /// Reference to the graph definition that produced this tree.
        /// </summary>
        public ParseableGraph SourceGraph { get; }

        /// <summary>
        /// Warnings generated during parsing (type coercion fallbacks, etc.).
        /// Does not include errors — parsing stops on errors.
        /// </summary>
        public IReadOnlyList<ValidationEntry> ParseWarnings { get; }
    }
}
