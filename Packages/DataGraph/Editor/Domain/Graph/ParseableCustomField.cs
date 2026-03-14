using System;
using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of a typed leaf field node.
    /// Reads a single cell value and coerces it to the target type.
    /// </summary>
    internal sealed class ParseableCustomField : ParseableNode
    {
        public ParseableCustomField(
            string fieldName,
            string column,
            FieldValueType valueType,
            string separator = null,
            string format = null,
            Type enumType = null)
            : base(fieldName, Array.Empty<ParseableNode>())
        {
            Column = column;
            ValueType = valueType;
            Separator = separator;
            Format = format;
            EnumType = enumType;
        }

        /// <summary>
        /// Column letter from which the value is read.
        /// </summary>
        public string Column { get; }

        /// <summary>
        /// The primitive type of this field's value.
        /// </summary>
        public FieldValueType ValueType { get; }

        /// <summary>
        /// Separator character for Vector2/Vector3 parsing.
        /// Null for non-vector types.
        /// </summary>
        public string Separator { get; }

        /// <summary>
        /// Format string for Color parsing (e.g. "hex", "rgba").
        /// Null for non-color types.
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// The concrete enum type for EnumFieldNode.
        /// Null for non-enum types.
        /// </summary>
        public Type EnumType { get; }
    }
}
