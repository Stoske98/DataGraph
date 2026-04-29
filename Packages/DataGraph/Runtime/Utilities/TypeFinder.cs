using System;
using System.Collections.Generic;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Locates a Type by name across all loaded assemblies, with a small cache
    /// for repeat lookups. Successful lookups are cached; null results are not
    /// cached, so types compiled after the first miss are still discoverable.
    /// Cache lives for the lifetime of the AppDomain — Unity domain reload
    /// resets it automatically.
    /// </summary>
    internal static class TypeFinder
    {
        private static readonly Dictionary<string, Type> _cache = new();

        /// <summary>
        /// Returns the first Type matching the fully qualified name across
        /// any loaded assembly, or null if not found.
        /// </summary>
        public static Type Find(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            if (_cache.TryGetValue(fullName, out var cached)) return cached;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                {
                    _cache[fullName] = type;
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries the DataGraph-generated namespace first (DataGraph.Data.X),
        /// then falls back to the bare name. Use for types produced by the
        /// CodeGenerator pipeline.
        /// </summary>
        public static Type FindGenerated(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            return Find("DataGraph.Data." + typeName) ?? Find(typeName);
        }
    }
}
