using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of a nested object field.
    /// Children define the properties of this object.
    /// Has no column — its value is a composite of child nodes.
    /// </summary>
    internal sealed class ParseableObjectField : ParseableNode
    {
        public ParseableObjectField(
            string fieldName,
            string typeName,
            IReadOnlyList<ParseableNode> children)
            : base(fieldName, children)
        {
            TypeName = typeName;
        }

        /// <summary>
        /// Base name for the generated nested class.
        /// </summary>
        public string TypeName { get; }
    }
}
