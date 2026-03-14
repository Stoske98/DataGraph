using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of a dictionary root node.
    /// Generates a key-based collection where each row maps to
    /// a key-value entry.
    /// </summary>
    internal sealed class ParseableDictionaryRoot : ParseableNode
    {
        public ParseableDictionaryRoot(
            string typeName,
            string keyColumn,
            KeyType keyType,
            IReadOnlyList<ParseableNode> children)
            : base(null, children)
        {
            TypeName = typeName;
            KeyColumn = keyColumn;
            KeyType = keyType;
        }

        /// <summary>
        /// Base name for generated classes (e.g. "Item" generates ItemSO).
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Column letter from which the dictionary key is read.
        /// </summary>
        public string KeyColumn { get; }

        /// <summary>
        /// Type of the dictionary key (Int or String).
        /// </summary>
        public KeyType KeyType { get; }
    }
}
