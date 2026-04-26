using System;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.CodeGen
{
    /// <summary>
    /// Maps domain types to C# type name strings used in generated code.
    /// </summary>
    internal static class TypeMapper
    {
        /// <summary>
        /// Returns the C# type name for a primitive field value type.
        /// </summary>
        public static string GetCSharpTypeName(FieldValueType valueType, Type enumType = null)
        {
            return valueType switch
            {
                FieldValueType.String => "string",
                FieldValueType.Int => "int",
                FieldValueType.Float => "float",
                FieldValueType.Double => "double",
                FieldValueType.Bool => "bool",
                FieldValueType.Vector2 => "Vector2",
                FieldValueType.Vector3 => "Vector3",
                FieldValueType.Color => "Color",
                FieldValueType.Enum => enumType?.Name ?? "int",
                _ => "object"
            };
        }

        /// <summary>
        /// Returns the C# type name for a dictionary key type.
        /// </summary>
        public static string GetKeyTypeName(KeyType keyType)
        {
            return keyType switch
            {
                KeyType.Int => "int",
                KeyType.String => "string",
                _ => "object"
            };
        }
    }
}
