using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Nodes;

namespace DataGraph.Editor.Adapter
{
    /// <summary>
    /// Validates DataGraph structure and reports issues through
    /// GTK GraphLogger for real-time error display on nodes.
    /// Called from DataGraphAsset.OnGraphChanged.
    /// </summary>
    internal sealed class GraphValidator
    {
        private readonly GraphLogger _logger;

        public GraphValidator(GraphLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Runs all structural and semantic validation rules on the graph.
        /// </summary>
        public void Validate(DataGraphAsset graph)
        {
            ValidateExactlyOneRoot(graph);
            ValidateAllNodesConnected(graph);
        }

        private void ValidateExactlyOneRoot(DataGraphAsset graph)
        {
            int rootCount = 0;
            for (int i = 0; i < graph.nodeCount; i++)
            {
                var inode = graph.GetNode(i);
                if (inode is DictionaryRootNode or ArrayRootNode or ObjectRootNode)
                    rootCount++;
            }

            if (rootCount == 0)
            {
                _logger.LogError("Graph must have exactly one Root node.");
            }
            else if (rootCount > 1)
            {
                _logger.LogError($"Graph must have exactly one Root node, but found {rootCount}.");
            }
        }

        private void ValidateAllNodesConnected(DataGraphAsset graph)
        {
            var connectedPorts = new List<IPort>();

            for (int i = 0; i < graph.nodeCount; i++)
            {
                var inode = graph.GetNode(i);
                if (inode is not Node node)
                    continue;
                if (node is DictionaryRootNode or ArrayRootNode or ObjectRootNode)
                    continue;

                var parentPort = node.GetInputPortByName("Parent");
                if (parentPort == null)
                    continue;

                connectedPorts.Clear();
                parentPort.GetConnectedPorts(connectedPorts);

                if (connectedPorts.Count == 0)
                {
                    _logger.LogWarning(
                        $"Node '{GetNodeDisplayName(node)}' is not connected to a parent.",
                        node);
                }
            }
        }

        private static string GetNodeDisplayName(Node node)
        {
            return node switch
            {
                StringFieldNode sf => sf.FieldName,
                NumberFieldNode nf => nf.FieldName,
                BoolFieldNode bf => bf.FieldName,
                Vector2FieldNode v2f => v2f.FieldName,
                Vector3FieldNode v3f => v3f.FieldName,
                ColorFieldNode clf => clf.FieldName,
                AssetFieldNode af => af.FieldName,
                ObjectFieldNode of => of.FieldName,
                ArrayFieldNode arf => arf.FieldName,
                DictionaryFieldNode df => df.FieldName,
                DictionaryRootNode dr => dr.TypeName,
                ArrayRootNode ar => ar.TypeName,
                ObjectRootNode or2 => or2.TypeName,
                _ => node.GetType().Name
            };
        }
    }
}
