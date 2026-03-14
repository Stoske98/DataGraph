using System;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// A terminal parsed value. Holds the type-coerced result
    /// of reading a single cell (e.g. int 42, string "Sword", etc.).
    /// </summary>
    internal sealed class ParsedValue : ParsedNode
    {
        public ParsedValue(string fieldName, object value, Type valueType)
            : base(fieldName)
        {
            Value = value;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        }

        /// <summary>
        /// The coerced value. May be null for nullable fields.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// The C# type of the value (e.g. typeof(int), typeof(string)).
        /// </summary>
        public Type ValueType { get; }
    }
}
