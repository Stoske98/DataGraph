using System.Collections.Generic;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Extracts the set of column letters referenced by nodes in a ParseableGraph.
    /// Used to build optimized range requests that fetch only needed columns.
    /// </summary>
    internal static class ColumnExtractor
    {
        /// <summary>
        /// Returns a sorted, deduplicated list of column letters used by the graph.
        /// Includes key columns, index columns, and all leaf field columns.
        /// Returns null if the graph is an Enum/Flag definition (no column filtering).
        /// </summary>
        internal static List<string> GetReferencedColumns(ParseableGraph graph)
        {
            if (graph?.Root == null) return null;
            if (graph.Root is ParseableEnumRoot || graph.Root is ParseableFlagRoot)
                return null;

            var columns = new HashSet<string>();
            CollectColumns(graph.Root, columns);

            if (columns.Count == 0) return null;

            var sorted = new List<string>(columns);
            sorted.Sort((a, b) =>
            {
                var idxA = Runtime.RawTableData.ColumnLetterToIndex(a);
                var idxB = Runtime.RawTableData.ColumnLetterToIndex(b);
                return idxA.CompareTo(idxB);
            });

            return sorted;
        }

        private static void CollectColumns(ParseableNode node, HashSet<string> columns)
        {
            switch (node)
            {
                case ParseableDictionaryRoot dict:
                    AddIfValid(columns, dict.KeyColumn);
                    break;
                case ParseableDictionaryField dict:
                    AddIfValid(columns, dict.KeyColumn);
                    break;
                case ParseableArrayField arr:
                    AddIfValid(columns, arr.IndexColumn);
                    break;
                case ParseableCustomField custom:
                    AddIfValid(columns, custom.Column);
                    break;
                case ParseableAssetField asset:
                    AddIfValid(columns, asset.Column);
                    break;
                case ParseableEnumField enumField:
                    AddIfValid(columns, enumField.Column);
                    break;
                case ParseableFlagField flagField:
                    AddIfValid(columns, flagField.Column);
                    break;
            }

            foreach (var child in node.Children)
                CollectColumns(child, columns);
        }

        private static void AddIfValid(HashSet<string> set, string column)
        {
            if (!string.IsNullOrEmpty(column))
                set.Add(column);
        }
    }
}
