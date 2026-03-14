using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of an object root node.
    /// Generates a single-object store for global configurations.
    /// </summary>
    internal sealed class ParseableObjectRoot : ParseableNode
    {
        public ParseableObjectRoot(
            string typeName,
            IReadOnlyList<ParseableNode> children)
            : base(null, children)
        {
            TypeName = typeName;
        }

        /// <summary>
        /// Base name for generated classes (e.g. "GameConfig" generates GameConfigSO).
        /// </summary>
        public string TypeName { get; }
    }
}
