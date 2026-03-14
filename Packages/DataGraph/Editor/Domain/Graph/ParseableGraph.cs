using System;
using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Immutable domain representation of a DataGraph parser graph.
    /// Produced by GraphModelAdapter from a GTK Graph.
    /// Consumed by ParserEngine, CodeGenerator, and all downstream systems.
    /// Has no GTK dependencies.
    /// </summary>
    internal sealed class ParseableGraph
    {
        public ParseableGraph(
            ParseableNode root,
            IReadOnlyList<ParseableNode> allNodes,
            string sheetId,
            int headerRowOffset,
            string graphName)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            AllNodes = allNodes ?? throw new ArgumentNullException(nameof(allNodes));
            SheetId = sheetId;
            HeaderRowOffset = headerRowOffset;
            GraphName = graphName;
        }

        /// <summary>
        /// The single root node of this graph.
        /// </summary>
        public ParseableNode Root { get; }

        /// <summary>
        /// All nodes in this graph, including root, in tree-traversal order.
        /// </summary>
        public IReadOnlyList<ParseableNode> AllNodes { get; }

        /// <summary>
        /// Identifier of the source sheet (URL, file path, or ID).
        /// </summary>
        public string SheetId { get; }

        /// <summary>
        /// Number of rows to skip before the first data row.
        /// </summary>
        public int HeaderRowOffset { get; }

        /// <summary>
        /// Name of this graph, used for file naming in code generation.
        /// </summary>
        public string GraphName { get; }
    }
}
