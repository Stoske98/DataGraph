using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// A parsed key-value collection.
    /// Keys are stored as objects (int or string) and values
    /// are ParsedNode instances.
    /// </summary>
    internal sealed class ParsedDictionary : ParsedNode
    {
        public ParsedDictionary(
            string fieldName,
            string keyTypeName,
            string valueTypeName,
            IReadOnlyDictionary<object, ParsedNode> entries)
            : base(fieldName)
        {
            KeyTypeName = keyTypeName;
            ValueTypeName = valueTypeName;
            Entries = entries;
        }

        /// <summary>
        /// C# type name of the keys (e.g. "int", "string").
        /// </summary>
        public string KeyTypeName { get; }

        /// <summary>
        /// Type name of the values (for code generation).
        /// </summary>
        public string ValueTypeName { get; }

        /// <summary>
        /// The parsed key-value entries.
        /// </summary>
        public IReadOnlyDictionary<object, ParsedNode> Entries { get; }
    }
}
