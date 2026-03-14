using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of a dictionary field within a parent object.
    /// Reads key-value pairs spread across multiple rows (vertical only).
    /// </summary>
    internal sealed class ParseableDictionaryField : ParseableNode
    {
        public ParseableDictionaryField(
            string fieldName,
            string typeName,
            string keyColumn,
            KeyType keyType,
            IReadOnlyList<ParseableNode> children)
            : base(fieldName, children)
        {
            TypeName = typeName;
            KeyColumn = keyColumn;
            KeyType = keyType;
        }

        /// <summary>
        /// Base name for the generated value class (if value is complex).
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
