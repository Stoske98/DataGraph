using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DataGraph.Editor.GraphView
{
    /// <summary>
    /// Custom GraphView canvas for DataGraph node editing.
    /// Enforces single root, single parent per child, proper edge sync.
    /// </summary>
    internal sealed class DataGraphView : UnityEditor.Experimental.GraphView.GraphView
    {
        private DataGraphAsset _graphAsset;
        private DataGraphSearchWindow _searchWindow;
        private Vector2 _cachedMouseGraphPos;

        /// <summary>
        /// Fired when edges or nodes are added/removed. Window subscribes to refresh JSON preview.
        /// </summary>
        public event Action OnGraphStructureChanged;

        public DataGraphAsset GraphAsset => _graphAsset;

        public DataGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            SetupGridBackground();

            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = OnSerializeElements;
            unserializeAndPaste = OnUnserializeAndPaste;
            deleteSelection = OnDeleteSelection;

            RegisterCallback<MouseMoveEvent>(evt =>
            {
                _cachedMouseGraphPos = contentViewContainer.WorldToLocal(evt.mousePosition);
            });

            // Ctrl+D to duplicate selected nodes
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.D && evt.ctrlKey)
                {
                    DuplicateSelection();
                    evt.StopPropagation();
                }
            });

            // Reload graph on undo/redo
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void SetupGridBackground()
        {
            // Load grid stylesheet
            var guids = UnityEditor.AssetDatabase.FindAssets("DataGraphGridStyle t:StyleSheet");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (uss != null)
                    styleSheets.Add(uss);
            }

            var grid = new GridBackground();
            grid.StretchToParentSize();
            Insert(0, grid);

            style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f);
        }

        /// <summary>
        /// Loads a DataGraphAsset and populates the view with nodes and edges.
        /// </summary>
        public void LoadGraph(DataGraphAsset graphAsset, bool panToRoot = true)
        {
            _graphAsset = graphAsset;
            ClearGraph();

            if (_graphAsset == null) return;

            var nodeViews = new Dictionary<string, DataGraphNodeView>();

            foreach (var nodeData in _graphAsset.Nodes)
            {
                var nodeView = CreateNodeView(nodeData);
                nodeViews[nodeData.Guid] = nodeView;
                AddElement(nodeView);
            }

            foreach (var edgeData in _graphAsset.Edges)
            {
                if (!nodeViews.TryGetValue(edgeData.OutputNodeGuid, out var outputNode)) continue;
                if (!nodeViews.TryGetValue(edgeData.InputNodeGuid, out var inputNode)) continue;

                var outputPort = outputNode.GetOutputPort(edgeData.OutputPortName);
                var inputPort = inputNode.GetInputPort(edgeData.InputPortName);
                if (outputPort == null || inputPort == null) continue;

                var edge = outputPort.ConnectTo(inputPort);
                AddElement(edge);
            }

            if (panToRoot)
                schedule.Execute(() => FrameOnRoot()).ExecuteLater(100);
        }

        /// <summary>
        /// Clears all elements from the view.
        /// </summary>
        public void ClearGraph()
        {
            graphElements.ForEach(e => RemoveElement(e));
        }

        /// <summary>
        /// Sets up the search window for node creation.
        /// </summary>
        public void SetSearchWindow(DataGraphSearchWindow searchWindow)
        {
            _searchWindow = searchWindow;

            nodeCreationRequest = ctx =>
            {
                if (_searchWindow == null) return;
                _searchWindow.CreationPosition = _cachedMouseGraphPos;
                SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _searchWindow);
            };
        }

        /// <summary>
        /// Creates a node at the given position and adds it to the graph.
        /// Enforces single root rule.
        /// </summary>
        public DataGraphNodeView CreateNode(string typeName, Vector2 position)
        {
            if (_graphAsset == null) return null;

            // Enforce single root
            if (NodeTypeRegistry.IsRootNode(typeName))
            {
                foreach (var node in _graphAsset.Nodes)
                {
                    if (NodeTypeRegistry.IsRootNode(node.TypeName))
                    {
                        EditorUtility.DisplayDialog("DataGraph",
                            "Graph already has a root node. Only one root is allowed.", "OK");
                        return null;
                    }
                }
            }

            Undo.RecordObject(_graphAsset, "Create Node");
            var nodeData = _graphAsset.AddNode(typeName, position);

            var defaults = NodeTypeRegistry.GetDefaultProperties(typeName);
            foreach (var kvp in defaults)
                nodeData.SetProperty(kvp.Key, kvp.Value);

            EditorUtility.SetDirty(_graphAsset);

            var nodeView = CreateNodeView(nodeData);
            AddElement(nodeView);
            return nodeView;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                if (port == startPort) return;
                if (port.node == startPort.node) return;
                if (port.direction == startPort.direction) return;
                compatiblePorts.Add(port);
            });
            return compatiblePorts;
        }

        /// <summary>
        /// Pans the view to center on the root node, or origin if no root.
        /// </summary>
        private void FrameOnRoot()
        {
            DataGraphNodeView rootView = null;
            nodes.ForEach(n =>
            {
                if (n is DataGraphNodeView dv && NodeTypeRegistry.IsRootNode(dv.NodeTypeName))
                    rootView = dv;
            });

            if (rootView != null)
            {
                var pos = rootView.GetPosition();
                var center = new Vector3(
                    pos.x + pos.width / 2f,
                    pos.y + pos.height / 2f, 0);
                UpdateViewTransform(
                    -center * contentViewContainer.transform.scale.x
                    + new Vector3(contentRect.width / 2f, contentRect.height / 2f, 0),
                    contentViewContainer.transform.scale);
            }
            else
            {
                UpdateViewTransform(
                    new Vector3(contentRect.width / 2f, contentRect.height / 2f, 0),
                    Vector3.one);
            }
        }

        private DataGraphNodeView CreateNodeView(SerializedNode nodeData)
        {
            return new DataGraphNodeView(nodeData, this);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_graphAsset == null) return change;

            // Handle removed elements (disconnect, delete edge)
            if (change.elementsToRemove != null)
            {
                Undo.RecordObject(_graphAsset, "Remove Elements");
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge)
                    {
                        var outputNode = edge.output?.node as DataGraphNodeView;
                        var inputNode = edge.input?.node as DataGraphNodeView;
                        if (outputNode != null && inputNode != null)
                        {
                            _graphAsset.RemoveEdge(
                                outputNode.NodeGuid, edge.output.portName,
                                inputNode.NodeGuid, edge.input.portName);
                        }
                    }
                    else if (element is DataGraphNodeView nodeView)
                    {
                        _graphAsset.RemoveNode(nodeView.NodeGuid);
                    }
                }
                EditorUtility.SetDirty(_graphAsset);
            }

            // Handle new edges
            if (change.edgesToCreate != null)
            {
                Undo.RecordObject(_graphAsset, "Create Edge");
                foreach (var edge in change.edgesToCreate)
                {
                    var outputNode = edge.output.node as DataGraphNodeView;
                    var inputNode = edge.input.node as DataGraphNodeView;
                    if (outputNode == null || inputNode == null) continue;

                    // Enforce single parent: remove existing edge to this input port
                    var inputPort = edge.input;
                    if (inputPort.capacity == Port.Capacity.Single)
                    {
                        var existingEdges = edges.ToList().Where(e =>
                            e.input == inputPort && e != edge).ToList();
                        foreach (var existing in existingEdges)
                        {
                            var oldOutput = existing.output?.node as DataGraphNodeView;
                            var oldInput = existing.input?.node as DataGraphNodeView;
                            if (oldOutput != null && oldInput != null)
                            {
                                _graphAsset.RemoveEdge(
                                    oldOutput.NodeGuid, existing.output.portName,
                                    oldInput.NodeGuid, existing.input.portName);
                            }
                            RemoveElement(existing);
                        }
                    }

                    _graphAsset.AddEdge(
                        outputNode.NodeGuid, edge.output.portName,
                        inputNode.NodeGuid, edge.input.portName);
                }
                EditorUtility.SetDirty(_graphAsset);
            }

            // Handle moved elements
            if (change.movedElements != null)
            {
                Undo.RecordObject(_graphAsset, "Move Nodes");
                foreach (var element in change.movedElements)
                {
                    if (element is DataGraphNodeView nodeView)
                    {
                        var nodeData = _graphAsset.FindNode(nodeView.NodeGuid);
                        if (nodeData != null)
                            nodeData.Position = nodeView.GetPosition().position;
                    }
                }
                EditorUtility.SetDirty(_graphAsset);
            }

            if (change.elementsToRemove != null || change.edgesToCreate != null)
                OnGraphStructureChanged?.Invoke();

            return change;
        }

        private void OnDeleteSelection(string operationName, AskUser askUser)
        {
            if (_graphAsset == null) return;

            Undo.RecordObject(_graphAsset, "Delete Elements");

            var edgesToRemove = selection.OfType<Edge>().ToList();
            var nodesToRemove = selection.OfType<DataGraphNodeView>().ToList();

            foreach (var edge in edgesToRemove)
            {
                var outputNode = edge.output?.node as DataGraphNodeView;
                var inputNode = edge.input?.node as DataGraphNodeView;
                if (outputNode != null && inputNode != null)
                {
                    _graphAsset.RemoveEdge(
                        outputNode.NodeGuid, edge.output.portName,
                        inputNode.NodeGuid, edge.input.portName);
                }
                RemoveElement(edge);
            }

            foreach (var nodeView in nodesToRemove)
            {
                var connectedEdges = edges.ToList().Where(e =>
                    e.output?.node == nodeView || e.input?.node == nodeView).ToList();
                foreach (var edge in connectedEdges)
                {
                    var outputNode = edge.output?.node as DataGraphNodeView;
                    var inputNode = edge.input?.node as DataGraphNodeView;
                    if (outputNode != null && inputNode != null)
                    {
                        _graphAsset.RemoveEdge(
                            outputNode.NodeGuid, edge.output.portName,
                            inputNode.NodeGuid, edge.input.portName);
                    }
                    RemoveElement(edge);
                }

                _graphAsset.RemoveNode(nodeView.NodeGuid);
                RemoveElement(nodeView);
            }

            EditorUtility.SetDirty(_graphAsset);
            OnGraphStructureChanged?.Invoke();
        }

        private void OnUndoRedo()
        {
            if (_graphAsset != null)
                LoadGraph(_graphAsset, panToRoot: false);
        }

        /// <summary>
        /// Duplicates selected nodes with offset, preserving connections between them.
        /// Root nodes are skipped (only one root allowed).
        /// </summary>
        private void DuplicateSelection()
        {
            if (_graphAsset == null) return;

            var selectedNodes = selection.OfType<DataGraphNodeView>().ToList();
            if (selectedNodes.Count == 0) return;

            Undo.RecordObject(_graphAsset, "Duplicate Nodes");

            var offset = new Vector2(50, 50);
            var guidMap = new Dictionary<string, string>();
            var newViews = new List<DataGraphNodeView>();

            // Pass 1: create duplicated nodes and views
            foreach (var sourceView in selectedNodes)
            {
                var sourceData = _graphAsset.FindNode(sourceView.NodeGuid);
                if (sourceData == null) continue;
                if (NodeTypeRegistry.IsRootNode(sourceData.TypeName)) continue;

                var newNode = _graphAsset.AddNode(sourceData.TypeName,
                    sourceData.Position + offset);

                foreach (var prop in sourceData.Properties)
                    newNode.SetProperty(prop.Key, prop.Value);

                guidMap[sourceData.Guid] = newNode.Guid;

                var nodeView = CreateNodeView(newNode);
                AddElement(nodeView);
                newViews.Add(nodeView);
            }

            // Pass 2: copy edges between duplicated nodes
            var originalEdges = _graphAsset.Edges.ToList();
            foreach (var edge in originalEdges)
            {
                if (guidMap.TryGetValue(edge.OutputNodeGuid, out var newOutput) &&
                    guidMap.TryGetValue(edge.InputNodeGuid, out var newInput))
                {
                    _graphAsset.AddEdge(newOutput, edge.OutputPortName,
                        newInput, edge.InputPortName);

                    // Create visual edge
                    var outView = newViews.Find(v => v.NodeGuid == newOutput);
                    var inView = newViews.Find(v => v.NodeGuid == newInput);
                    if (outView != null && inView != null)
                    {
                        var outPort = outView.GetOutputPort(edge.OutputPortName);
                        var inPort = inView.GetInputPort(edge.InputPortName);
                        if (outPort != null && inPort != null)
                        {
                            var visualEdge = outPort.ConnectTo(inPort);
                            AddElement(visualEdge);
                        }
                    }
                }
            }

            EditorUtility.SetDirty(_graphAsset);

            // Select duplicated nodes
            ClearSelection();
            foreach (var view in newViews)
                AddToSelection(view);

            OnGraphStructureChanged?.Invoke();
        }

        /// <summary>
        /// Unsubscribe from Undo on cleanup.
        /// </summary>
        ~DataGraphView()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private string OnSerializeElements(IEnumerable<GraphElement> elements) => "";
        private void OnUnserializeAndPaste(string operationName, string data) { }
    }
}
