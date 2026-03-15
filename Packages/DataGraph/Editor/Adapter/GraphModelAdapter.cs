using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Nodes;
using DataGraph.Runtime;

namespace DataGraph.Editor.Adapter
{
    /// <summary>
    /// Reads a GTK Graph (DataGraphAsset) and produces a ParseableGraph
    /// domain model. This is the boundary between GTK presentation layer
    /// and the domain layer.
    /// </summary>
    internal sealed class GraphModelAdapter
    {
        /// <summary>
        /// Converts a DataGraphAsset into a ParseableGraph.
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
                var parseableRoot = ConvertNode(rootNode, childMap);

                var allNodes = new List<ParseableNode>();
                CollectNodes(parseableRoot, allNodes);

                var graph = new ParseableGraph(
                    parseableRoot,
                    allNodes,
                    graphAsset.SheetId,
                    graphAsset.HeaderRowOffset,
                    graphAsset.GraphName);

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
                var inode = graphAsset.GetNode(i);
                if (inode is not Node node) continue;
                if (!IsRootNode(node)) continue;

                if (root != null)
                    return null;
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
        /// Builds a map from each parent node to its connected child nodes
        /// by traversing Children output port connections.
        /// </summary>
        private Dictionary<Node, List<Node>> BuildChildMap(DataGraphAsset graphAsset)
        {
            var map = new Dictionary<Node, List<Node>>();
            var connectedPorts = new List<IPort>();

            for (int i = 0; i < graphAsset.nodeCount; i++)
            {
                var inode = graphAsset.GetNode(i);
                if (inode is not Node node) continue;

                var outputPort = node.GetOutputPortByName("Children");
                if (outputPort == null) continue;

                connectedPorts.Clear();
                outputPort.GetConnectedPorts(connectedPorts);

                var children = new List<Node>();
                foreach (var connectedPort in connectedPorts)
                {
                    var childNode = FindNodeOwningPort(connectedPort, graphAsset);
                    if (childNode != null)
                        children.Add(childNode);
                }

                map[node] = children;
            }

            return map;
        }

        /// <summary>
        /// Finds the Node that owns a given port by checking all nodes'
        /// input ports for reference equality.
        /// </summary>
        private static Node FindNodeOwningPort(IPort port, DataGraphAsset graphAsset)
        {
            for (int i = 0; i < graphAsset.nodeCount; i++)
            {
                var inode = graphAsset.GetNode(i);
                if (inode is not Node node) continue;

                var parentPort = node.GetInputPortByName("Parent");
                if (parentPort != null && ReferenceEquals(parentPort, port))
                    return node;
            }
            return null;
        }

        private ParseableNode ConvertNode(Node node, Dictionary<Node, List<Node>> childMap)
        {
            var children = ConvertChildren(node, childMap);

            return node switch
            {
                DictionaryRootNode dict => new ParseableDictionaryRoot(
                    dict.TypeName, dict.KeyColumn, dict.KeyType, children),

                ArrayRootNode arr => new ParseableArrayRoot(
                    arr.TypeName, children),

                ObjectRootNode obj => new ParseableObjectRoot(
                    obj.TypeName, children),

                ObjectFieldNode objField => new ParseableObjectField(
                    objField.FieldName, objField.TypeName, children),

                ArrayFieldNode arrField => new ParseableArrayField(
                    arrField.FieldName,
                    arrField.TypeName,
                    arrField.ArrayMode,
                    arrField.ArrayMode == ArrayMode.Vertical ? arrField.IndexColumn : null,
                    arrField.ArrayMode == ArrayMode.Horizontal ? arrField.Separator : null,
                    children),

                DictionaryFieldNode dictField => new ParseableDictionaryField(
                    dictField.FieldName,
                    dictField.TypeName,
                    dictField.KeyColumn,
                    dictField.KeyType,
                    children),

                CustomFieldNode custom => new ParseableCustomField(
                    custom.FieldName,
                    custom.Column,
                    custom.ValueType,
                    custom.Separator,
                    custom.Format,
                    custom.ResolveEnumType()),

                AssetFieldNode asset => new ParseableAssetField(
                    asset.FieldName,
                    asset.Column,
                    asset.AssetTypeName,
                    asset.LoadMethod),

                _ => throw new InvalidOperationException(
                    $"Unknown node type: {node.GetType().Name}")
            };
        }

        private ParseableNode[] ConvertChildren(Node parent, Dictionary<Node, List<Node>> childMap)
        {
            if (!childMap.TryGetValue(parent, out var gtkChildren) || gtkChildren.Count == 0)
                return Array.Empty<ParseableNode>();

            var result = new ParseableNode[gtkChildren.Count];
            for (int i = 0; i < gtkChildren.Count; i++)
            {
                result[i] = ConvertNode(gtkChildren[i], childMap);
            }
            return result;
        }

        private static void CollectNodes(ParseableNode node, List<ParseableNode> result)
        {
            result.Add(node);
            foreach (var child in node.Children)
            {
                CollectNodes(child, result);
            }
        }
    }
}
