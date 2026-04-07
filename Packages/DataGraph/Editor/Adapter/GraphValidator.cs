using System.Collections.Generic;

namespace DataGraph.Editor.Adapter
{
    /// <summary>
    /// Validates DataGraph structure and reports issues.
    /// Checks root count, connectivity, and column validity.
    /// </summary>
    internal sealed class GraphValidator
    {
        public sealed class ValidationResult
        {
            private readonly List<string> _errors = new();
            private readonly List<string> _warnings = new();

            public IReadOnlyList<string> Errors => _errors;
            public IReadOnlyList<string> Warnings => _warnings;
            public bool IsValid => _errors.Count == 0;

            public void AddError(string message) => _errors.Add(message);
            public void AddWarning(string message) => _warnings.Add(message);
        }

        public ValidationResult Validate(DataGraphAsset graph)
        {
            var result = new ValidationResult();
            ValidateRootNode(graph, result);
            ValidateConnectivity(graph, result);
            ValidateColumns(graph, result);
            return result;
        }

        private static void ValidateRootNode(DataGraphAsset graph, ValidationResult result)
        {
            int rootCount = 0;
            foreach (var node in graph.Nodes)
                if (NodeTypeRegistry.IsRoot(node.TypeName))
                    rootCount++;

            if (rootCount == 0)
                result.AddError("Graph must have exactly one Root node.");
            else if (rootCount > 1)
                result.AddError($"Graph has {rootCount} root nodes — must have exactly one.");
        }

        private static void ValidateConnectivity(DataGraphAsset graph, ValidationResult result)
        {
            var connectedInputs = new HashSet<string>();
            foreach (var edge in graph.Edges)
                connectedInputs.Add(edge.InputNodeGuid);

            foreach (var node in graph.Nodes)
            {
                if (NodeTypeRegistry.IsRoot(node.TypeName)) continue;
                if (!connectedInputs.Contains(node.Guid))
                {
                    var name = node.GetProperty("FieldName", node.TypeName);
                    result.AddWarning($"Node '{name}' is not connected to a parent.");
                }
            }
        }

        private static void ValidateColumns(DataGraphAsset graph, ValidationResult result)
        {
            if (graph.CachedHeaders.Count == 0) return;

            var validHeaders = new HashSet<string>();
            foreach (var h in graph.CachedHeaders) validHeaders.Add(h);
            for (int i = 0; i < 26; i++) validHeaders.Add(((char)('A' + i)).ToString());

            foreach (var node in graph.Nodes)
            {
                CheckColumn(node, "Column", validHeaders, result);
                CheckColumn(node, "KeyColumn", validHeaders, result);
                CheckColumn(node, "IndexColumn", validHeaders, result);
                CheckColumn(node, "NameColumn", validHeaders, result);
                CheckColumn(node, "ValueColumn", validHeaders, result);
            }
        }

        private static void CheckColumn(SerializedNode node, string propKey,
            HashSet<string> valid, ValidationResult result)
        {
            var value = node.GetProperty(propKey, null);
            if (value == null) return;
            if (!valid.Contains(value))
            {
                var name = node.GetProperty("FieldName", node.GetProperty("TypeName", node.TypeName));
                result.AddWarning($"Node '{name}': column '{value}' not found in sheet headers.");
            }
        }
    }
}
