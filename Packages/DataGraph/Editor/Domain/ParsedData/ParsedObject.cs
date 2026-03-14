using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// A parsed composite object containing named child fields.
    /// Corresponds to root entries and nested object fields.
    /// </summary>
    internal sealed class ParsedObject : ParsedNode
    {
        public ParsedObject(
            string fieldName,
            string typeName,
            IReadOnlyList<ParsedNode> children)
            : base(fieldName)
        {
            TypeName = typeName;
            Children = children;
        }

        /// <summary>
        /// Type name of this object (used for code generation).
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// The parsed child fields of this object.
        /// </summary>
        public IReadOnlyList<ParsedNode> Children { get; }
    }
}
