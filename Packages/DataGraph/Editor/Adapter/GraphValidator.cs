using System.Collections.Generic;
using DataGraph.Runtime;

namespace DataGraph.Editor.Adapter
{
    /// <summary>
    /// Validates DataGraph structure and reports issues.
    /// Checks root count, connectivity, required fields (TypeName, FieldName),
    /// and column validity.
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
            ValidateRequiredFields(graph, result);
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
                    var name = GetNodeDisplayName(node);
                    result.AddWarning($"Node '{name}' is not connected to a parent.");
                }
            }
        }

        private static void ValidateRequiredFields(DataGraphAsset graph, ValidationResult result)
        {
            foreach (var node in graph.Nodes)
            {
                if (NodeTypeRegistry.IsRoot(node.TypeName)) continue;

                if (node.TypeName is NodeTypeRegistry.Types.Object
                    or NodeTypeRegistry.Types.Enum
                    or NodeTypeRegistry.Types.Flag)
                {
                    var typeName = node.GetProperty("TypeName", "");
                    if (string.IsNullOrEmpty(typeName))
                    {
                        result.AddError(
                            $"{NodeTypeRegistry.GetDisplayTitle(node.TypeName)} node is missing required Type Name.");
                    }
                }

                var parentType = graph.GetParentTypeName(node.Guid);
                if (NodeTypeRegistry.ShouldShowFieldName(parentType))
                {
                    var fieldName = node.GetProperty("FieldName", "");
                    if (string.IsNullOrEmpty(fieldName))
                    {
                        var display = GetNodeDisplayName(node);
                        result.AddError(
                            $"Node '{display}' is missing required Field Name.");
                    }
                }
            }
        }

        private static void ValidateColumns(DataGraphAsset graph, ValidationResult result)
        {
            if (graph.CachedHeaders.Count == 0) return;

            var validHeaders = new HashSet<string>();
            foreach (var h in graph.CachedHeaders) validHeaders.Add(h);
            for (int i = 0; i < graph.CachedHeaders.Count; i++)
                validHeaders.Add(RawTableData.IndexToColumnLetter(i));

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
                var name = GetNodeDisplayName(node);
                result.AddWarning($"Node '{name}': column '{value}' not found in sheet headers.");
            }
        }

        private static string GetNodeDisplayName(SerializedNode node)
        {
            var fieldName = node.GetProperty("FieldName", "");
            if (!string.IsNullOrEmpty(fieldName)) return fieldName;
            var typeName = node.GetProperty("TypeName", "");
            if (!string.IsNullOrEmpty(typeName)) return typeName;
            return NodeTypeRegistry.GetDisplayTitle(node.TypeName);
        }
    }
}
