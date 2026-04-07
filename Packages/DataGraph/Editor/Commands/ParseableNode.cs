using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Abstract base for all nodes in the ParseableGraph domain model.
    /// Carries the field name and child references common to all node kinds.
    /// </summary>
    internal abstract class ParseableNode
    {
        protected ParseableNode(string fieldName, IReadOnlyList<ParseableNode> children)
        {
            FieldName = fieldName;
            Children = children ?? System.Array.Empty<ParseableNode>();
        }

        /// <summary>
        /// Name of the field this node represents in its parent structure.
        /// Null for root nodes.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Child nodes that define sub-fields or elements.
        /// Empty for leaf nodes.
        /// </summary>
        public IReadOnlyList<ParseableNode> Children { get; }

        /// <summary>
        /// True if this node has no children (leaf node).
        /// </summary>
        public bool IsLeaf => Children.Count == 0;
    }
}
