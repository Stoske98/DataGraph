using DataGraph.Editor.Domain;

namespace DataGraph.Editor.CodeGen
{
    /// <summary>
    /// Shared helpers used by all code generators (SO / Blob / Quantum).
    /// </summary>
    internal static class CodeGenHelpers
    {
        /// <summary>
        /// True if <paramref name="node"/> has at least one child that
        /// represents a nested structure. Array/Dictionary fields with a
        /// TypeName are always structural; for other nodes a single
        /// CustomField child is treated as a leaf scalar (not structural).
        /// </summary>
        public static bool HasStructuralChildren(ParseableNode node)
        {
            var typeName = node switch
            {
                ParseableArrayField arr => arr.TypeName,
                ParseableDictionaryField dict => dict.TypeName,
                _ => null
            };
            if (!string.IsNullOrEmpty(typeName)) return node.Children.Count > 0;
            if (node.Children.Count == 1 && node.Children[0] is ParseableCustomField)
                return false;
            return node.Children.Count > 0;
        }
    }
}
