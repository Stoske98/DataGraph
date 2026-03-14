using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of an array root node.
    /// Generates a sequential collection where each row is one element.
    /// </summary>
    internal sealed class ParseableArrayRoot : ParseableNode
    {
        public ParseableArrayRoot(
            string typeName,
            IReadOnlyList<ParseableNode> children)
            : base(null, children)
        {
            TypeName = typeName;
        }

        /// <summary>
        /// Base name for generated classes (e.g. "Level" generates LevelSO).
        /// </summary>
        public string TypeName { get; }
    }
}
