using System;
using System.Collections.Generic;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Adapter
{
    /// <summary>
    /// Reads a DataGraphAsset and produces an immutable ParseableGraph.
    /// No GTK dependency — reads SerializedNode and SerializedEdge directly.
    /// Output is identical to the old GTK adapter — downstream pipeline unchanged.
    /// </summary>
    internal sealed class GraphModelAdapter
    {
        /// <summary>
        /// Converts a DataGraphAsset into an immutable ParseableGraph.
        /// </summary>
        public Result<ParseableGraph> ReadGraph(DataGraphAsset graphAsset)
        {
            if (graphAsset == null)
                return Result<ParseableGraph>.Failure("Graph asset is null.");

            try
            {
                var rootNode = FindRootNode(graphAsset);
                if (rootNode == null)
                    return Result<ParseableGraph>.Failure("Graph must have exactly one Root node.");

                var childMap = BuildChildMap(graphAsset);
                var parseableRoot = ConvertNode(rootNode, childMap, graphAsset);

                var allNodes = new List<ParseableNode>();
                CollectNodes(parseableRoot, allNodes);

                var graphName = !string.IsNullOrEmpty(graphAsset.GraphName)
                    ? graphAsset.GraphName
                    : graphAsset.name;

                var graph = new ParseableGraph(
                    parseableRoot,
                    allNodes,
                    graphAsset.SheetId,
                    graphAsset.HeaderRowOffset,
                    graphName,
                    graphAsset.SheetName);

                return Result<ParseableGraph>.Success(graph);
            }
            catch (Exception ex)
            {
                return Result<ParseableGraph>.Failure($"Adapter error: {ex.Message}");
            }
        }

        private static SerializedNode FindRootNode(DataGraphAsset graph)
        {
            foreach (var node in graph.Nodes)
                if (NodeTypeRegistry.IsRootNode(node.TypeName))
                    return node;
            return null;
        }

        /// <summary>
        /// Builds parent-to-children map from edges.
        /// Output node = parent, Input node = child.
        /// </summary>
        private static Dictionary<string, List<SerializedNode>> BuildChildMap(DataGraphAsset graph)
        {
            var map = new Dictionary<string, List<SerializedNode>>();
            foreach (var edge in graph.Edges)
            {
                if (!map.ContainsKey(edge.OutputNodeGuid))
                    map[edge.OutputNodeGuid] = new List<SerializedNode>();
                var child = graph.FindNode(edge.InputNodeGuid);
                if (child != null)
                    map[edge.OutputNodeGuid].Add(child);
            }
            return map;
        }

        private ParseableNode ConvertNode(SerializedNode node,
            Dictionary<string, List<SerializedNode>> childMap,
            DataGraphAsset graph)
        {
            var children = ConvertChildren(node.Guid, childMap, graph);

            var rootTypeName = graph.GraphName ?? "Unnamed";

            return node.TypeName switch
            {
                NodeTypeRegistry.Types.DictionaryRoot => new ParseableDictionaryRoot(
                    rootTypeName,
                    ResolveColumn(node.GetProperty("KeyColumn"), graph),
                    ParseKeyType(node.GetProperty("KeyType")),
                    children),

                NodeTypeRegistry.Types.ArrayRoot => new ParseableArrayRoot(
                    rootTypeName,
                    children),

                NodeTypeRegistry.Types.ObjectRoot => new ParseableObjectRoot(
                    rootTypeName,
                    children),

                NodeTypeRegistry.Types.EnumRoot => new ParseableEnumRoot(
                    rootTypeName,
                    ResolveColumn(node.GetProperty("NameColumn"), graph),
                    ResolveColumn(node.GetProperty("ValueColumn"), graph)),

                NodeTypeRegistry.Types.FlagRoot => new ParseableFlagRoot(
                    rootTypeName,
                    ResolveColumn(node.GetProperty("NameColumn"), graph),
                    ResolveColumn(node.GetProperty("ValueColumn"), graph)),

                NodeTypeRegistry.Types.ObjectField => new ParseableObjectField(
                    node.GetProperty("FieldName"),
                    node.GetProperty("TypeName"),
                    children),

                NodeTypeRegistry.Types.VerticalArrayField => new ParseableArrayField(
                    node.GetProperty("FieldName"),
                    node.GetProperty("TypeName"),
                    ArrayMode.Vertical,
                    ResolveColumn(node.GetProperty("IndexColumn"), graph),
                    null,
                    children),

                NodeTypeRegistry.Types.HorizontalArrayField => new ParseableArrayField(
                    node.GetProperty("FieldName"),
                    null,
                    ArrayMode.Horizontal,
                    null,
                    node.GetProperty("Separator", ","),
                    children),

                NodeTypeRegistry.Types.DictionaryField => new ParseableDictionaryField(
                    node.GetProperty("FieldName"),
                    node.GetProperty("TypeName"),
                    ResolveColumn(node.GetProperty("KeyColumn"), graph),
                    ParseKeyType(node.GetProperty("KeyType")),
                    children),

                NodeTypeRegistry.Types.StringField => new ParseableCustomField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    FieldValueType.String, null, null, null),

                NodeTypeRegistry.Types.NumberField => CreateNumberField(node, graph),

                NodeTypeRegistry.Types.BoolField => new ParseableCustomField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    FieldValueType.Bool, null, null, null),

                NodeTypeRegistry.Types.Vector2Field => new ParseableCustomField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    FieldValueType.Vector2,
                    node.GetProperty("Separator", ","), null, null),

                NodeTypeRegistry.Types.Vector3Field => new ParseableCustomField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    FieldValueType.Vector3,
                    node.GetProperty("Separator", ","), null, null),

                NodeTypeRegistry.Types.ColorField => new ParseableCustomField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    FieldValueType.Color, null,
                    node.GetProperty("Format", "Hex"), null),

                NodeTypeRegistry.Types.AssetField => new ParseableAssetField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    ParseAssetType(node.GetProperty("AssetType", "Sprite"))),

                NodeTypeRegistry.Types.EnumField => new ParseableEnumField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    node.GetProperty("EnumTypeName")),

                NodeTypeRegistry.Types.FlagField => new ParseableFlagField(
                    node.GetProperty("FieldName"),
                    ResolveColumn(node.GetProperty("Column"), graph),
                    node.GetProperty("FlagTypeName"),
                    node.GetProperty("Separator", ",")),

                _ => throw new InvalidOperationException($"Unknown node type: {node.TypeName}")
            };
        }

        private ParseableNode[] ConvertChildren(string parentGuid,
            Dictionary<string, List<SerializedNode>> childMap,
            DataGraphAsset graph)
        {
            if (!childMap.TryGetValue(parentGuid, out var childNodes) || childNodes.Count == 0)
                return Array.Empty<ParseableNode>();

            var result = new ParseableNode[childNodes.Count];
            for (int i = 0; i < childNodes.Count; i++)
                result[i] = ConvertNode(childNodes[i], childMap, graph);
            return result;
        }

        private static ParseableCustomField CreateNumberField(SerializedNode node, DataGraphAsset graph)
        {
            var numberType = node.GetProperty("NumberType", "Int");
            var fieldValueType = numberType switch
            {
                "Float" => FieldValueType.Float,
                "Double" => FieldValueType.Double,
                _ => FieldValueType.Int
            };
            return new ParseableCustomField(
                node.GetProperty("FieldName"),
                ResolveColumn(node.GetProperty("Column"), graph),
                fieldValueType, null, null, null);
        }

        /// <summary>
        /// Resolves a column reference. If value matches a cached header name,
        /// converts to column letter. Otherwise returns as-is.
        /// </summary>
        private static string ResolveColumn(string columnValue, DataGraphAsset graph)
        {
            if (string.IsNullOrEmpty(columnValue)) return "A";

            var headers = graph.CachedHeaders;
            for (int i = 0; i < headers.Count; i++)
            {
                if (string.Equals(headers[i], columnValue, StringComparison.OrdinalIgnoreCase))
                    return RawTableData.IndexToColumnLetter(i);
            }
            return columnValue;
        }

        private static KeyType ParseKeyType(string value)
        {
            return value == "String" ? KeyType.String : KeyType.Int;
        }

        private static AssetType ParseAssetType(string value)
        {
            if (Enum.TryParse<AssetType>(value, out var result))
                return result;
            return AssetType.Sprite;
        }

        private static void CollectNodes(ParseableNode node, List<ParseableNode> result)
        {
            result.Add(node);
            foreach (var child in node.Children)
                CollectNodes(child, result);
        }
    }
}
