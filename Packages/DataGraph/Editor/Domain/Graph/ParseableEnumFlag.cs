namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Immutable domain representation of an enum definition root node.
    /// </summary>
    internal sealed class ParseableEnumRoot : ParseableNode
    {
        public ParseableEnumRoot(string typeName, string nameColumn, string valueColumn)
            : base(null, System.Array.Empty<ParseableNode>())
        {
            TypeName = typeName;
            NameColumn = nameColumn;
            ValueColumn = valueColumn;
        }

        public string TypeName { get; }
        public string NameColumn { get; }
        public string ValueColumn { get; }
    }

    /// <summary>
    /// Immutable domain representation of a [Flags] enum definition root node.
    /// </summary>
    internal sealed class ParseableFlagRoot : ParseableNode
    {
        public ParseableFlagRoot(string typeName, string nameColumn, string valueColumn)
            : base(null, System.Array.Empty<ParseableNode>())
        {
            TypeName = typeName;
            NameColumn = nameColumn;
            ValueColumn = valueColumn;
        }

        public string TypeName { get; }
        public string NameColumn { get; }
        public string ValueColumn { get; }
    }

    /// <summary>
    /// Immutable domain representation of an enum field leaf node.
    /// </summary>
    internal sealed class ParseableEnumField : ParseableNode
    {
        public ParseableEnumField(string fieldName, string column, string enumTypeName)
            : base(fieldName, System.Array.Empty<ParseableNode>())
        {
            Column = column;
            EnumTypeName = enumTypeName;
        }

        public string Column { get; }
        public string EnumTypeName { get; }
    }

    /// <summary>
    /// Immutable domain representation of a flags field leaf node.
    /// </summary>
    internal sealed class ParseableFlagField : ParseableNode
    {
        public ParseableFlagField(string fieldName, string column, string flagTypeName, string separator)
            : base(fieldName, System.Array.Empty<ParseableNode>())
        {
            Column = column;
            FlagTypeName = flagTypeName;
            Separator = separator;
        }

        public string Column { get; }
        public string FlagTypeName { get; }
        public string Separator { get; }
    }
}
