using System;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Extracts the user-defined TypeName from a graph's root node.
    /// All three root variants (Dictionary, Array, Object) carry a TypeName;
    /// this helper centralizes the switch and surfaces unknown root kinds
    /// as an exception so missing cases fail loudly during code generation
    /// rather than producing a silent "Unknown" type name.
    /// </summary>
    internal static class RootTypeResolver
    {
        /// <summary>
        /// Returns the TypeName of the root node. Throws
        /// <see cref="InvalidOperationException"/> for unrecognized roots.
        /// </summary>
        public static string GetTypeName(ParseableNode root)
        {
            return root switch
            {
                ParseableDictionaryRoot dict => dict.TypeName,
                ParseableArrayRoot arr => arr.TypeName,
                ParseableObjectRoot obj => obj.TypeName,
                _ => throw new InvalidOperationException(
                    $"Unknown root node type: {root?.GetType().Name ?? "null"}")
            };
        }
    }
}
