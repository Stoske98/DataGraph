using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Nodes;
using DataGraph.Runtime;

namespace DataGraph.Editor.Adapter
{
    /// <summary>
    /// Reads a GTK Graph (DataGraphAsset) and produces a ParseableGraph
    /// domain model. Uses reflection to traverse GTK internal wire models
    /// since the public port API does not resolve connections when graphs
    /// are loaded programmatically.
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

                var childMap = BuildChildMapViaReflection(graphAsset);
                var parseableRoot = ConvertNode(rootNode, childMap);

                var allNodes = new List<ParseableNode>();
                CollectNodes(parseableRoot, allNodes);

                var graph = new ParseableGraph(
                    parseableRoot,
                    allNodes,
                    graphAsset.SheetId,
                    graphAsset.HeaderRowOffset,
                    graphAsset.GraphName,
                    graphAsset.SheetName);

                return Result<ParseableGraph>.Success(graph);
            }
            catch (Exception ex)
            {
                return Result<ParseableGraph>.Failure($"Graph adaptation failed: {ex.Message}");
            }
        }

        private Node FindRootNode(DataGraphAsset graphAsset)
        {
            Node root = null;
            for (int i = 0; i < graphAsset.nodeCount; i++)
            {
                if (graphAsset.GetNode(i) is not Node node) continue;
                if (!IsRootNode(node)) continue;
                if (root != null) return null;
                root = node;
            }
            return root;
        }

        private static bool IsRootNode(Node node)
        {
            return node is DictionaryRootNode
                or ArrayRootNode
                or ObjectRootNode;
        }

        /// <summary>
        /// Builds parent-to-children map by traversing GTK wire models via reflection.
        /// Path: Graph.m_Implementation -> GraphModel.NodeModels + GraphModel.WireModels.
        /// Each WireModel has FromPort.NodeModel and ToPort.NodeModel which map to
        /// User Nodes through IUserNodeModelImp.Node.
        /// </summary>
        private Dictionary<Node, List<Node>> BuildChildMapViaReflection(DataGraphAsset graphAsset)
        {
            var map = new Dictionary<Node, List<Node>>();

            var implField = typeof(Graph).GetField("m_Implementation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var impl = implField?.GetValue(graphAsset);
            if (impl == null) return map;

            var nodeModelToUserNode = new Dictionary<object, Node>();
            var nodeModelsProp = impl.GetType().GetProperty("NodeModels",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (nodeModelsProp != null)
            {
                var nodeModels = nodeModelsProp.GetValue(impl) as IList;
                if (nodeModels != null)
                {
                    foreach (var nm in nodeModels)
                    {
                        if (nm == null) continue;
                        var userNode = GetUserNodeFromNodeModel(nm);
                        if (userNode != null)
                            nodeModelToUserNode[nm] = userNode;
                    }
                }
            }

            var wireModelsProp = impl.GetType().GetProperty("WireModels",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var wireModels = wireModelsProp?.GetValue(impl) as IList;

            if (wireModels == null || wireModels.Count == 0) return map;

            var guidToUserNode = new Dictionary<string, Node>();
            foreach (var kvp in nodeModelToUserNode)
            {
                var guid = GetGuidString(kvp.Key);
                if (!string.IsNullOrEmpty(guid))
                    guidToUserNode[guid] = kvp.Value;
            }

            foreach (var wire in wireModels)
            {
                if (wire == null) continue;

                var fromGuid = GetWireNodeGuid(wire, "FromNodeGuid");
                var toGuid = GetWireNodeGuid(wire, "ToNodeGuid");

                if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid)) continue;

                if (!guidToUserNode.TryGetValue(fromGuid, out var parentNode)) continue;
                if (!guidToUserNode.TryGetValue(toGuid, out var childNode)) continue;

                if (!map.TryGetValue(parentNode, out var children))
                {
                    children = new List<Node>();
                    map[parentNode] = children;
                }

                if (!children.Contains(childNode))
                    children.Add(childNode);
            }

            return map;
        }

        /// <summary>
        /// Extracts the User Node from an internal NodeModel via IUserNodeModelImp.Node property.
        /// </summary>
        private static Node GetUserNodeFromNodeModel(object nodeModel)
        {
            var nodeProp = nodeModel.GetType().GetProperty("Node",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (nodeProp == null) return null;
            return nodeProp.GetValue(nodeModel) as Node;
        }

        /// <summary>
        /// Gets a GUID string from a wire's node reference property (FromNodeGuid or ToNodeGuid).
        /// </summary>
        private static string GetWireNodeGuid(object wire, string propertyName)
        {
            var prop = wire.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop == null)
            {
                var field = wire.GetType().GetField(propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(wire)?.ToString();
            }
            return prop.GetValue(wire)?.ToString();
        }

        /// <summary>
        /// Gets the GUID string from a NodeModel's Guid property.
        /// </summary>
        private static string GetGuidString(object nodeModel)
        {
            var guidProp = nodeModel.GetType().GetProperty("Guid",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return guidProp?.GetValue(nodeModel)?.ToString();
        }

        private ParseableNode ConvertNode(Node node, Dictionary<Node, List<Node>> childMap)
        {
            node.DefineNode();
            var children = ConvertChildren(node, childMap);

            return node switch
            {
                DictionaryRootNode => new ParseableDictionaryRoot(
                    GetOption<string>(node, "TypeName"),
                    GetColumnOption(node, "KeyColumn"),
                    GetOption<KeyType>(node, "KeyType"),
                    children),

                ArrayRootNode => new ParseableArrayRoot(
                    GetOption<string>(node, "TypeName"),
                    children),

                ObjectRootNode => new ParseableObjectRoot(
                    GetOption<string>(node, "TypeName"),
                    children),

                ObjectFieldNode => new ParseableObjectField(
                    GetOption<string>(node, "FieldName"),
                    GetOption<string>(node, "TypeName"),
                    children),

                ArrayFieldNode => CreateArrayField(node, children),

                DictionaryFieldNode => new ParseableDictionaryField(
                    GetOption<string>(node, "FieldName"),
                    GetOption<string>(node, "TypeName"),
                    GetColumnOption(node, "KeyColumn"),
                    GetOption<KeyType>(node, "KeyType"),
                    children),

                CustomFieldNode => CreateCustomField(node),

                AssetFieldNode => new ParseableAssetField(
                    GetOption<string>(node, "FieldName"),
                    GetColumnOption(node, "Column"),
                    GetOption<string>(node, "AssetType"),
                    GetOption<AssetLoadMethod>(node, "LoadMethod")),

                _ => throw new InvalidOperationException(
                    $"Unknown node type: {node.GetType().Name}")
            };
        }

        private ParseableArrayField CreateArrayField(Node node, ParseableNode[] children)
        {
            var mode = GetOption<ArrayMode>(node, "ArrayMode");
            return new ParseableArrayField(
                GetOption<string>(node, "FieldName"),
                GetOption<string>(node, "TypeName"),
                mode,
                mode == ArrayMode.Vertical ? GetColumnOption(node, "IndexColumn") : null,
                mode == ArrayMode.Horizontal ? GetOption<string>(node, "Separator") : null,
                children);
        }

        private ParseableCustomField CreateCustomField(Node node)
        {
            var enumTypeName = GetOption<string>(node, "EnumType");
            Type enumType = null;
            if (!string.IsNullOrEmpty(enumTypeName))
                enumType = Type.GetType(enumTypeName);

            return new ParseableCustomField(
                GetOption<string>(node, "FieldName"),
                GetColumnOption(node, "Column"),
                GetOption<FieldValueType>(node, "ValueType"),
                GetOption<string>(node, "Separator"),
                GetOption<string>(node, "Format"),
                enumType);
        }

        /// <summary>
        /// Reads a GTK node option value by name. Returns default(T) if not found.
        /// </summary>
        private static T GetOption<T>(Node node, string optionName)
        {
            var option = node.GetNodeOptionByName(optionName);
            if (option != null && option.TryGetValue<T>(out var value))
                return value;
            return default;
        }

        /// <summary>
        /// Reads a column option as string. Returns "A" if not found.
        /// </summary>
        private static string GetColumnOption(Node node, string optionName)
        {
            var option = node.GetNodeOptionByName(optionName);
            if (option != null && option.TryGetValue<string>(out var col) && !string.IsNullOrEmpty(col))
                return col;
            return "A";
        }

        private ParseableNode[] ConvertChildren(Node parent, Dictionary<Node, List<Node>> childMap)
        {
            if (!childMap.TryGetValue(parent, out var gtkChildren) || gtkChildren.Count == 0)
                return Array.Empty<ParseableNode>();

            var result = new ParseableNode[gtkChildren.Count];
            for (int i = 0; i < gtkChildren.Count; i++)
                result[i] = ConvertNode(gtkChildren[i], childMap);
            return result;
        }

        private static void CollectNodes(ParseableNode node, List<ParseableNode> result)
        {
            result.Add(node);
            foreach (var child in node.Children)
                CollectNodes(child, result);
        }
    }
}
