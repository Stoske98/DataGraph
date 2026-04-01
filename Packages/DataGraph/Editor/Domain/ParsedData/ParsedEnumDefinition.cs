using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Parsed result of an EnumRoot or FlagRoot graph.
    /// Contains the ordered list of name-value members
    /// extracted from the table data.
    /// </summary>
    internal sealed class ParsedEnumDefinition : ParsedNode
    {
        public ParsedEnumDefinition(
            string typeName,
            bool isFlags,
            IReadOnlyList<EnumMember> members)
            : base(null)
        {
            TypeName = typeName;
            IsFlags = isFlags;
            Members = members;
        }

        public string TypeName { get; }
        public bool IsFlags { get; }
        public IReadOnlyList<EnumMember> Members { get; }
    }

    /// <summary>
    /// A single enum member with a name and integer value.
    /// </summary>
    internal readonly struct EnumMember
    {
        public EnumMember(string name, int value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public int Value { get; }
    }
}
