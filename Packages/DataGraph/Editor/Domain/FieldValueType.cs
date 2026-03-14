namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Primitive value types supported by custom field nodes.
    /// Each maps to a concrete C# type in generated code.
    /// </summary>
    internal enum FieldValueType
    {
        String,
        Int,
        Float,
        Bool,
        Vector2,
        Vector3,
        Color,
        Enum
    }
}
