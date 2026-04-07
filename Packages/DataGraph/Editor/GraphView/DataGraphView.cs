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
    /// v2: Fixed nodes/edges, single-child enforcement, dynamic property refresh.
    /// </summary>
    internal sealed class DataGraphView : UnityEditor.Experimental.GraphView.GraphView
    {
        private DataGraphAsset _graphAsset;
        private DataGraphSearchWindow _searchWindow;
        private Vector2 _cachedMouseGraphPos;
        public event Action OnGraphStructureChanged;
        private IVisualElementScheduledItem _propertyChangedDebounce;

        public void NotifyPropertyChanged() { _propertyChangedDebounce?.Pause(); OnGraphStructureChanged?.Invoke(); }
        public void NotifyPropertyChangedDeferred()
        {
            _propertyChangedDebounce?.Pause();
            _propertyChangedDebounce = schedule.Execute(() => OnGraphStructureChanged?.Invoke());
            _propertyChangedDebounce.ExecuteLater(300);
        }

        public DataGraphAsset GraphAsset => _graphAsset;

        public DataGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            SetupGridBackground();
            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = _ => "";
            unserializeAndPaste = (_, _) => { };
            deleteSelection = OnDeleteSelection;
            RegisterCallback<MouseMoveEvent>(e => _cachedMouseGraphPos = contentViewContainer.WorldToLocal(e.mousePosition));
            RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.D && e.ctrlKey) { DuplicateSelection(); e.StopPropagation(); } });
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void SetupGridBackground()
        {
            var guids = AssetDatabase.FindAssets("DataGraphGridStyle t:StyleSheet");
            if (guids.Length > 0) { var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0])); if (uss != null) styleSheets.Add(uss); }
            var grid = new GridBackground(); grid.StretchToParentSize(); Insert(0, grid);
            style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f);
        }

        public void LoadGraph(DataGraphAsset graphAsset, bool panToRoot = true)
        {
            _graphAsset = graphAsset; ClearGraph();
            if (_graphAsset == null) return;
            var views = new Dictionary<string, DataGraphNodeView>();
            foreach (var nd in _graphAsset.Nodes) { var v = new DataGraphNodeView(nd, this); views[nd.Guid] = v; AddElement(v); }
            foreach (var ed in _graphAsset.Edges)
            {
                if (!views.TryGetValue(ed.OutputNodeGuid, out var ov)) continue;
                if (!views.TryGetValue(ed.InputNodeGuid, out var iv)) continue;
                var op = ov.GetOutputPort(ed.OutputPortName); var ip = iv.GetInputPort(ed.InputPortName);
                if (op != null && ip != null) AddElement(op.ConnectTo(ip));
            }
            if (panToRoot) schedule.Execute(() => FrameOnRoot()).ExecuteLater(100);
        }

        public void ClearGraph() { graphElements.ForEach(e => RemoveElement(e)); }

        public void SetSearchWindow(DataGraphSearchWindow sw)
        {
            _searchWindow = sw;
            nodeCreationRequest = ctx => { if (_searchWindow == null) return; _searchWindow.CreationPosition = _cachedMouseGraphPos; SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _searchWindow); };
        }

        public DataGraphNodeView CreateNode(string typeName, Vector2 position)
        {
            if (_graphAsset == null) return null;
            if (NodeTypeRegistry.IsRoot(typeName) || NodeTypeRegistry.IsDefinition(typeName))
            { EditorUtility.DisplayDialog("DataGraph", "This node type is created automatically with the graph.", "OK"); return null; }
            Undo.RecordObject(_graphAsset, "Create Node");
            var nd = _graphAsset.AddNode(typeName, position);
            foreach (var kv in NodeTypeRegistry.GetDefaultProperties(typeName)) nd.SetProperty(kv.Key, kv.Value);
            EditorUtility.SetDirty(_graphAsset);
            var v = new DataGraphNodeView(nd, this); AddElement(v); return v;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var r = new List<Port>();
            ports.ForEach(p => { if (p != startPort && p.node != startPort.node && p.direction != startPort.direction) r.Add(p); });
            return r;
        }

        private void FrameOnRoot()
        {
            DataGraphNodeView rv = null;
            nodes.ForEach(n => { if (n is DataGraphNodeView dv && NodeTypeRegistry.IsRoot(dv.NodeTypeName)) rv = dv; });
            if (rv == null) return;
            var pos = rv.GetPosition();
            var center = new Vector3(pos.x + pos.width / 2f, pos.y + pos.height / 2f, 0);
            UpdateViewTransform(-center * contentViewContainer.transform.scale.x + new Vector3(layout.width / 2f, layout.height / 2f, 0), contentViewContainer.transform.scale);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_graphAsset == null) return change;
            bool structureChanged = false;
            var toRefresh = new HashSet<string>();

            if (change.elementsToRemove != null)
            {
                Undo.RecordObject(_graphAsset, "Delete Elements");
                var skip = new List<GraphElement>();
                foreach (var el in change.elementsToRemove)
                {
                    if (el is Edge edge)
                    {
                        var ov = edge.output?.node as DataGraphNodeView; var iv = edge.input?.node as DataGraphNodeView;
                        if (ov != null && iv != null)
                        {
                            if (IsEdgeFixed(ov.NodeGuid, edge.output.portName, iv.NodeGuid, edge.input.portName)) { skip.Add(el); continue; }
                            _graphAsset.RemoveEdge(ov.NodeGuid, edge.output.portName, iv.NodeGuid, edge.input.portName);
                            toRefresh.Add(iv.NodeGuid);
                        }
                    }
                    else if (el is DataGraphNodeView nv) { if (nv.IsFixed) { skip.Add(el); continue; } _graphAsset.RemoveNode(nv.NodeGuid); }
                }
                foreach (var s in skip) change.elementsToRemove.Remove(s);
                EditorUtility.SetDirty(_graphAsset); structureChanged = true;
            }

            if (change.edgesToCreate != null)
            {
                Undo.RecordObject(_graphAsset, "Create Edge");
                foreach (var edge in change.edgesToCreate)
                {
                    var ov = edge.output.node as DataGraphNodeView; var iv = edge.input.node as DataGraphNodeView;
                    if (ov == null || iv == null) continue;

                    if (edge.input.capacity == Port.Capacity.Single)
                        foreach (var ex in edges.ToList().Where(e => e.input == edge.input && e != edge).ToList())
                        {
                            var xo = ex.output?.node as DataGraphNodeView; var xi = ex.input?.node as DataGraphNodeView;
                            if (xo != null && xi != null && !IsEdgeFixed(xo.NodeGuid, ex.output.portName, xi.NodeGuid, ex.input.portName))
                                _graphAsset.RemoveEdge(xo.NodeGuid, ex.output.portName, xi.NodeGuid, ex.input.portName);
                            RemoveElement(ex);
                        }

                    if (edge.output.capacity == Port.Capacity.Single)
                        foreach (var ex in edges.ToList().Where(e => e.output == edge.output && e != edge).ToList())
                        {
                            var xo = ex.output?.node as DataGraphNodeView; var xi = ex.input?.node as DataGraphNodeView;
                            if (xo != null && xi != null && !IsEdgeFixed(xo.NodeGuid, ex.output.portName, xi.NodeGuid, ex.input.portName))
                            { _graphAsset.RemoveEdge(xo.NodeGuid, ex.output.portName, xi.NodeGuid, ex.input.portName); toRefresh.Add(xi.NodeGuid); }
                            RemoveElement(ex);
                        }

                    _graphAsset.AddEdge(ov.NodeGuid, edge.output.portName, iv.NodeGuid, edge.input.portName);
                    toRefresh.Add(iv.NodeGuid);
                }
                EditorUtility.SetDirty(_graphAsset); structureChanged = true;
            }

            if (change.movedElements != null)
            {
                Undo.RecordObject(_graphAsset, "Move Nodes");
                foreach (var el in change.movedElements)
                    if (el is DataGraphNodeView nv) { var nd = _graphAsset.FindNode(nv.NodeGuid); if (nd != null) nd.Position = nv.GetPosition().position; }
                EditorUtility.SetDirty(_graphAsset);
            }

            if (toRefresh.Count > 0)
                nodes.ForEach(n => { if (n is DataGraphNodeView dv && toRefresh.Contains(dv.NodeGuid)) dv.RefreshPropertyControls(); });

            if (structureChanged) OnGraphStructureChanged?.Invoke();
            return change;
        }

        private void OnDeleteSelection(string operationName, AskUser askUser)
        {
            if (_graphAsset == null) return;
            Undo.RecordObject(_graphAsset, "Delete Elements");
            var toRefresh = new HashSet<string>();

            foreach (var edge in selection.OfType<Edge>().ToList())
            {
                var ov = edge.output?.node as DataGraphNodeView; var iv = edge.input?.node as DataGraphNodeView;
                if (ov != null && iv != null)
                {
                    if (IsEdgeFixed(ov.NodeGuid, edge.output.portName, iv.NodeGuid, edge.input.portName)) continue;
                    _graphAsset.RemoveEdge(ov.NodeGuid, edge.output.portName, iv.NodeGuid, edge.input.portName);
                    toRefresh.Add(iv.NodeGuid);
                }
                RemoveElement(edge);
            }

            foreach (var nv in selection.OfType<DataGraphNodeView>().Where(n => !n.IsFixed).ToList())
            {
                foreach (var edge in edges.ToList().Where(e => e.output?.node == nv || e.input?.node == nv).ToList())
                {
                    var ov = edge.output?.node as DataGraphNodeView; var iv = edge.input?.node as DataGraphNodeView;
                    if (ov != null && iv != null) _graphAsset.RemoveEdge(ov.NodeGuid, edge.output.portName, iv.NodeGuid, edge.input.portName);
                    RemoveElement(edge);
                }
                _graphAsset.RemoveNode(nv.NodeGuid); RemoveElement(nv);
            }

            EditorUtility.SetDirty(_graphAsset);
            if (toRefresh.Count > 0) nodes.ForEach(n => { if (n is DataGraphNodeView dv && toRefresh.Contains(dv.NodeGuid)) dv.RefreshPropertyControls(); });
            OnGraphStructureChanged?.Invoke();
        }

        private bool IsEdgeFixed(string og, string op, string ig, string ip)
        {
            foreach (var e in _graphAsset.Edges)
                if (e.OutputNodeGuid == og && e.OutputPortName == op && e.InputNodeGuid == ig && e.InputPortName == ip) return e.IsFixed;
            return false;
        }

        private void OnUndoRedo() { if (_graphAsset != null) LoadGraph(_graphAsset, panToRoot: false); }

        private void DuplicateSelection()
        {
            if (_graphAsset == null) return;
            var sel = selection.OfType<DataGraphNodeView>().Where(n => !n.IsFixed).ToList();
            if (sel.Count == 0) return;
            Undo.RecordObject(_graphAsset, "Duplicate Nodes");
            var map = new Dictionary<string, string>(); var newViews = new List<DataGraphNodeView>();

            foreach (var sv in sel)
            {
                var sd = _graphAsset.FindNode(sv.NodeGuid); if (sd == null) continue;
                var nn = _graphAsset.AddNode(sd.TypeName, sd.Position + new Vector2(50, 50));
                foreach (var p in sd.Properties) nn.SetProperty(p.Key, p.Value);
                map[sd.Guid] = nn.Guid;
                var nv = new DataGraphNodeView(nn, this); AddElement(nv); newViews.Add(nv);
            }

            foreach (var e in _graphAsset.Edges.ToList())
                if (map.TryGetValue(e.OutputNodeGuid, out var no) && map.TryGetValue(e.InputNodeGuid, out var ni))
                {
                    _graphAsset.AddEdge(no, e.OutputPortName, ni, e.InputPortName);
                    var ov = newViews.Find(v => v.NodeGuid == no); var iv = newViews.Find(v => v.NodeGuid == ni);
                    if (ov != null && iv != null) { var op = ov.GetOutputPort(e.OutputPortName); var ip = iv.GetInputPort(e.InputPortName); if (op != null && ip != null) AddElement(op.ConnectTo(ip)); }
                }

            EditorUtility.SetDirty(_graphAsset);
            ClearSelection(); foreach (var v in newViews) AddToSelection(v);
            OnGraphStructureChanged?.Invoke();
        }

        ~DataGraphView() { Undo.undoRedoPerformed -= OnUndoRedo; }
    }
}
