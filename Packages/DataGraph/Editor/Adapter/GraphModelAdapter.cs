using System;
using System.Collections.Generic;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Adapter
{
    /// <summary>
    /// Reads a DataGraphAsset and produces an immutable ParseableGraph.
    /// v2: Unified node types. TypeName resolved from Object/Enum/Flag nodes
    /// with fallback to GraphName when empty.
    /// </summary>
    internal sealed class GraphModelAdapter
    {
        public Result<ParseableGraph> ReadGraph(DataGraphAsset graphAsset)
        {
            if (graphAsset == null)
                return Result<ParseableGraph>.Failure("Graph asset is null.");

            try
            {
                var rootNode = FindRootNode(graphAsset);
                if (rootNode == null)
                    return Result<ParseableGraph>.Failure("Graph has no Root node.");

                var childMap = BuildChildMap(graphAsset);
                var rootChild = GetSingleChild(rootNode.Guid, childMap);
                if (rootChild == null)
                    return Result<ParseableGraph>.Failure("Root node has no connected structure.");

                var graphName = !string.IsNullOrEmpty(graphAsset.GraphName)
                    ? graphAsset.GraphName : graphAsset.name;

                var parseableRoot = ConvertRootChild(rootChild, childMap, graphAsset, graphName);

                var allNodes = new List<ParseableNode>();
                CollectNodes(parseableRoot, allNodes);

                return Result<ParseableGraph>.Success(new ParseableGraph(
                    parseableRoot, allNodes, graphAsset.SheetId,
                    graphAsset.HeaderRowOffset, graphName, graphAsset.SheetName));
            }
            catch (Exception ex)
            {
                return Result<ParseableGraph>.Failure($"Adapter error: {ex.Message}");
            }
        }

        private static string ResolveTypeName(SerializedNode node, string fallback)
        {
            var t = node?.GetProperty("TypeName", "");
            return string.IsNullOrEmpty(t) ? fallback : t;
        }

        private ParseableNode ConvertRootChild(
            SerializedNode node, Dictionary<string, List<SerializedNode>> childMap,
            DataGraphAsset graph, string graphName)
        {
            switch (node.TypeName)
            {
                case NodeTypeRegistry.Types.Dictionary:
                {
                    var objectChild = GetSingleChild(node.Guid, childMap);
                    var typeName = ResolveTypeName(objectChild, graphName);
                    var children = objectChild != null
                        ? ConvertChildren(objectChild.Guid, childMap, graph)
                        : Array.Empty<ParseableNode>();
                    return new ParseableDictionaryRoot(typeName,
                        ResolveColumn(node.GetProperty("KeyColumn"), graph),
                        ParseKeyType(node.GetProperty("KeyType")), children);
                }

                case NodeTypeRegistry.Types.VerticalArray:
                {
                    var objectChild = GetSingleChild(node.Guid, childMap);
                    var typeName = ResolveTypeName(objectChild, graphName);
                    var children = objectChild != null
                        ? ConvertChildren(objectChild.Guid, childMap, graph)
                        : Array.Empty<ParseableNode>();
                    return new ParseableArrayRoot(typeName, children);
                }

                case NodeTypeRegistry.Types.Object:
                {
                    var typeName = ResolveTypeName(node, graphName);
                    return new ParseableObjectRoot(typeName,
                        ConvertChildren(node.Guid, childMap, graph));
                }

                case NodeTypeRegistry.Types.Enum:
                {
                    var typeName = ResolveTypeName(node, graphName);
                    return new ParseableEnumRoot(typeName,
                        ResolveColumn(node.GetProperty("NameColumn"), graph),
                        ResolveColumn(node.GetProperty("ValueColumn"), graph));
                }

                case NodeTypeRegistry.Types.Flag:
                {
                    var typeName = ResolveTypeName(node, graphName);
                    return new ParseableFlagRoot(typeName,
                        ResolveColumn(node.GetProperty("NameColumn"), graph),
                        ResolveColumn(node.GetProperty("ValueColumn"), graph));
                }

                default:
                    throw new InvalidOperationException(
                        $"Unexpected root child type: {node.TypeName}");
            }
        }

        private ParseableNode ConvertNode(
            SerializedNode node, Dictionary<string, List<SerializedNode>> childMap,
            DataGraphAsset graph)
        {
            switch (node.TypeName)
            {
                case NodeTypeRegistry.Types.Object:
                    return new ParseableObjectField(
                        node.GetProperty("FieldName"),
                        node.GetProperty("TypeName", ""),
                        ConvertChildren(node.Guid, childMap, graph));

                case NodeTypeRegistry.Types.VerticalArray:
                {
                    var singleChild = GetSingleChild(node.Guid, childMap);

                    if (singleChild != null && singleChild.TypeName == NodeTypeRegistry.Types.Object)
                    {
                        // VerticalArray -> Object: leaf children go directly into array
                        // TypeName comes from Object node
                        return new ParseableArrayField(
                            node.GetProperty("FieldName"),
                            singleChild.GetProperty("TypeName", "Element"),
                            ArrayMode.Vertical,
                            ResolveColumn(node.GetProperty("IndexColumn"), graph),
                            null,
                            ConvertChildren(singleChild.Guid, childMap, graph));
                    }

                    if (singleChild != null)
                    {
                        // VerticalArray -> primitive leaf (no Object)
                        return new ParseableArrayField(
                            node.GetProperty("FieldName"), null,
                            ArrayMode.Vertical,
                            ResolveColumn(node.GetProperty("IndexColumn"), graph),
                            null,
                            new[] { ConvertNode(singleChild, childMap, graph) });
                    }

                    return new ParseableArrayField(
                        node.GetProperty("FieldName"), null,
                        ArrayMode.Vertical,
                        ResolveColumn(node.GetProperty("IndexColumn"), graph),
                        null, Array.Empty<ParseableNode>());
                }

                case NodeTypeRegistry.Types.HorizontalArray:
                {
                    var singleChild = GetSingleChild(node.Guid, childMap);
                    var children = singleChild != null
                        ? new[] { ConvertNode(singleChild, childMap, graph) }
                        : Array.Empty<ParseableNode>();
                    return new ParseableArrayField(
                        node.GetProperty("FieldName"), null,
                        ArrayMode.Horizontal, null,
                        node.GetProperty("Separator", ","), children);
                }

                case NodeTypeRegistry.Types.Dictionary:
                {
                    var singleChild = GetSingleChild(node.Guid, childMap);

                    if (singleChild != null && singleChild.TypeName == NodeTypeRegistry.Types.Object)
                    {
                        return new ParseableDictionaryField(
                            node.GetProperty("FieldName"),
                            singleChild.GetProperty("TypeName", "Entry"),
                            ResolveColumn(node.GetProperty("KeyColumn"), graph),
                            ParseKeyType(node.GetProperty("KeyType")),
                            ConvertChildren(singleChild.Guid, childMap, graph));
                    }

                    if (singleChild != null)
                    {
                        return new ParseableDictionaryField(
                            node.GetProperty("FieldName"), null,
                            ResolveColumn(node.GetProperty("KeyColumn"), graph),
                            ParseKeyType(node.GetProperty("KeyType")),
                            new[] { ConvertNode(singleChild, childMap, graph) });
                    }

                    return new ParseableDictionaryField(
                        node.GetProperty("FieldName"), null,
                        ResolveColumn(node.GetProperty("KeyColumn"), graph),
                        ParseKeyType(node.GetProperty("KeyType")),
                        Array.Empty<ParseableNode>());
                }

                case NodeTypeRegistry.Types.StringField:
                    return new ParseableCustomField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph), FieldValueType.String);

                case NodeTypeRegistry.Types.NumberField:
                {
                    var ft = node.GetProperty("NumberType", "Int") switch
                    { "Float" => FieldValueType.Float, "Double" => FieldValueType.Double, _ => FieldValueType.Int };
                    return new ParseableCustomField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph), ft);
                }

                case NodeTypeRegistry.Types.BoolField:
                    return new ParseableCustomField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph), FieldValueType.Bool);

                case NodeTypeRegistry.Types.Vector2Field:
                    return new ParseableCustomField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph), FieldValueType.Vector2,
                        node.GetProperty("Separator", ","));

                case NodeTypeRegistry.Types.Vector3Field:
                    return new ParseableCustomField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph), FieldValueType.Vector3,
                        node.GetProperty("Separator", ","));

                case NodeTypeRegistry.Types.ColorField:
                    return new ParseableCustomField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph), FieldValueType.Color,
                        null, node.GetProperty("Format", "Hex"));

                case NodeTypeRegistry.Types.AssetField:
                    return new ParseableAssetField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph),
                        ParseAssetType(node.GetProperty("AssetType", "Sprite")));

                case NodeTypeRegistry.Types.EnumField:
                    return new ParseableEnumField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph),
                        node.GetProperty("EnumTypeName"));

                case NodeTypeRegistry.Types.FlagField:
                    return new ParseableFlagField(node.GetProperty("FieldName"),
                        ResolveColumn(node.GetProperty("Column"), graph),
                        node.GetProperty("FlagTypeName"),
                        node.GetProperty("Separator", ","));

                default:
                    throw new InvalidOperationException($"Unknown node type: {node.TypeName}");
            }
        }

        private static SerializedNode FindRootNode(DataGraphAsset g)
        {
            foreach (var n in g.Nodes) if (n.TypeName == NodeTypeRegistry.Types.Root) return n;
            return null;
        }

        private static Dictionary<string, List<SerializedNode>> BuildChildMap(DataGraphAsset g)
        {
            var map = new Dictionary<string, List<SerializedNode>>();
            foreach (var e in g.Edges)
            {
                if (!map.ContainsKey(e.OutputNodeGuid)) map[e.OutputNodeGuid] = new List<SerializedNode>();
                var child = g.FindNode(e.InputNodeGuid);
                if (child != null) map[e.OutputNodeGuid].Add(child);
            }
            return map;
        }

        private static SerializedNode GetSingleChild(string guid, Dictionary<string, List<SerializedNode>> map)
        {
            if (!map.TryGetValue(guid, out var c)) return null;
            return c.Count > 0 ? c[0] : null;
        }

        private ParseableNode[] ConvertChildren(string guid, Dictionary<string, List<SerializedNode>> map, DataGraphAsset g)
        {
            if (!map.TryGetValue(guid, out var c) || c.Count == 0) return Array.Empty<ParseableNode>();
            var r = new ParseableNode[c.Count];
            for (int i = 0; i < c.Count; i++) r[i] = ConvertNode(c[i], map, g);
            return r;
        }

        private static string ResolveColumn(string v, DataGraphAsset g)
        {
            if (string.IsNullOrEmpty(v)) return "A";
            for (int i = 0; i < g.CachedHeaders.Count; i++)
                if (string.Equals(g.CachedHeaders[i], v, StringComparison.OrdinalIgnoreCase))
                    return RawTableData.IndexToColumnLetter(i);
            return v;
        }

        private static KeyType ParseKeyType(string v) => v == "String" ? KeyType.String : KeyType.Int;

        private static AssetType ParseAssetType(string v)
        {
            if (Enum.TryParse<AssetType>(v, out var r)) return r;
            return AssetType.Sprite;
        }

        private static void CollectNodes(ParseableNode n, List<ParseableNode> r)
        {
            r.Add(n);
            foreach (var c in n.Children) CollectNodes(c, r);
        }
    }
}
