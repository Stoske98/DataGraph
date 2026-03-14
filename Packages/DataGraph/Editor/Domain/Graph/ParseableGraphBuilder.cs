using System;
using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Fluent builder for constructing ParseableGraph instances.
    /// Used in tests and by GraphModelAdapter to build graphs
    /// without direct GTK dependency.
    /// </summary>
    internal sealed class ParseableGraphBuilder
    {
        private ParseableNode _root;
        private string _sheetId = "";
        private int _headerRowOffset = 1;
        private string _graphName = "Unnamed";

        public ParseableGraphBuilder WithSheetId(string sheetId)
        {
            _sheetId = sheetId;
            return this;
        }

        public ParseableGraphBuilder WithHeaderRowOffset(int offset)
        {
            _headerRowOffset = offset;
            return this;
        }

        public ParseableGraphBuilder WithGraphName(string name)
        {
            _graphName = name;
            return this;
        }

        public ParseableGraphBuilder WithRoot(ParseableNode root)
        {
            _root = root;
            return this;
        }

        public ParseableGraph Build()
        {
            if (_root == null)
                throw new InvalidOperationException("Root node must be set before building.");

            var allNodes = new List<ParseableNode>();
            CollectNodes(_root, allNodes);

            return new ParseableGraph(_root, allNodes, _sheetId, _headerRowOffset, _graphName);
        }

        private static void CollectNodes(ParseableNode node, List<ParseableNode> result)
        {
            result.Add(node);
            foreach (var child in node.Children)
            {
                CollectNodes(child, result);
            }
        }
    }
}
