using System;
using System.Collections.Generic;
using UnityEngine;

namespace DataGraph.Editor
{
    /// <summary>
    /// ScriptableObject that stores a complete DataGraph definition.
    /// GraphType determines the initial node structure.
    /// Fixed nodes cannot be deleted.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGraph", menuName = "DataGraph/Graph")]
    internal sealed class DataGraphAsset : ScriptableObject
    {
        [SerializeField] private string _graphName = "";
        [SerializeField] private string _sheetId = "";
        [SerializeField] private string _sheetName = "Sheet1";
        [SerializeField] private int _headerRowOffset = 1;
        [SerializeField] private GraphType _graphType = GraphType.Dictionary;
        [SerializeField] private List<SerializedNode> _nodes = new();
        [SerializeField] private List<SerializedEdge> _edges = new();
        [SerializeField] private List<string> _cachedHeaders = new();
        [SerializeField] private long _lastFetchTimeTicks;

        public string GraphName { get => _graphName; set => _graphName = value; }
        public string SheetId { get => _sheetId; set => _sheetId = value; }
        public string SheetName { get => _sheetName; set => _sheetName = value; }
        public int HeaderRowOffset { get => _headerRowOffset; set => _headerRowOffset = value; }
        public GraphType GraphType { get => _graphType; set => _graphType = value; }
        public List<SerializedNode> Nodes => _nodes;
        public List<SerializedEdge> Edges => _edges;
        public IReadOnlyList<string> CachedHeaders => _cachedHeaders;
        public DateTime LastFetchTime => new DateTime(_lastFetchTimeTicks);

        public void UpdateCachedHeaders(IReadOnlyList<string> headers)
        {
            _cachedHeaders.Clear();
            if (headers != null)
                foreach (var h in headers)
                    _cachedHeaders.Add(h);
            _lastFetchTimeTicks = DateTime.Now.Ticks;
        }

        public List<string> GetColumnChoices()
        {
            if (_cachedHeaders.Count > 0)
                return new List<string>(_cachedHeaders);
            var fallback = new List<string>();
            for (int i = 0; i < 26; i++)
                fallback.Add(((char)('A' + i)).ToString());
            return fallback;
        }

        public SerializedNode FindNode(string guid)
        {
            foreach (var node in _nodes)
                if (node.Guid == guid) return node;
            return null;
        }

        public SerializedNode AddNode(string typeName, Vector2 position, bool isFixed = false)
        {
            var node = new SerializedNode
            {
                Guid = System.Guid.NewGuid().ToString(),
                TypeName = typeName,
                Position = position,
                IsFixed = isFixed
            };
            _nodes.Add(node);
            return node;
        }

        public void RemoveNode(string guid)
        {
            _nodes.RemoveAll(n => n.Guid == guid && !n.IsFixed);
            _edges.RemoveAll(e => e.OutputNodeGuid == guid || e.InputNodeGuid == guid);
        }

        public SerializedEdge AddEdge(string outputNodeGuid, string outputPortName,
            string inputNodeGuid, string inputPortName, bool isFixed = false)
        {
            var edge = new SerializedEdge
            {
                OutputNodeGuid = outputNodeGuid,
                OutputPortName = outputPortName,
                InputNodeGuid = inputNodeGuid,
                InputPortName = inputPortName,
                IsFixed = isFixed
            };
            _edges.Add(edge);
            return edge;
        }

        public void RemoveEdge(string outputNodeGuid, string outputPortName,
            string inputNodeGuid, string inputPortName)
        {
            _edges.RemoveAll(e =>
                e.OutputNodeGuid == outputNodeGuid &&
                e.OutputPortName == outputPortName &&
                e.InputNodeGuid == inputNodeGuid &&
                e.InputPortName == inputPortName &&
                !e.IsFixed);
        }

        public List<SerializedEdge> GetChildEdges(string parentNodeGuid)
        {
            var result = new List<SerializedEdge>();
            foreach (var edge in _edges)
                if (edge.OutputNodeGuid == parentNodeGuid)
                    result.Add(edge);
            return result;
        }

        public string GetParentGuid(string childGuid)
        {
            foreach (var edge in _edges)
                if (edge.InputNodeGuid == childGuid)
                    return edge.OutputNodeGuid;
            return null;
        }

        public string GetParentTypeName(string childGuid)
        {
            var parentGuid = GetParentGuid(childGuid);
            if (parentGuid == null) return null;
            return FindNode(parentGuid)?.TypeName;
        }

        /// <summary>
        /// Creates the initial fixed node structure based on GraphType.
        /// </summary>
        public void InitializeStructure(string graphName)
        {
            _graphName = graphName;
            _nodes.Clear();
            _edges.Clear();

            var rootPos = new Vector2(-46, -42.5f);
            var step = new Vector2(120, 0);

            switch (_graphType)
            {
                case GraphType.Dictionary:
                {
                    var root = AddNode(NodeTypeRegistry.Types.Root, rootPos, isFixed: true);
                    var dict = AddNode(NodeTypeRegistry.Types.Dictionary, rootPos + step, isFixed: true);
                    dict.SetProperty("KeyColumn", "A");
                    dict.SetProperty("KeyType", "Int");
                    var obj = AddNode(NodeTypeRegistry.Types.Object, rootPos + step * 3, isFixed: true);
                    obj.SetProperty("TypeName", "");
                    AddEdge(root.Guid, "Children", dict.Guid, "Parent", isFixed: true);
                    AddEdge(dict.Guid, "Children", obj.Guid, "Parent", isFixed: true);
                    break;
                }

                case GraphType.Array:
                {
                    var root = AddNode(NodeTypeRegistry.Types.Root, rootPos, isFixed: true);
                    var arr = AddNode(NodeTypeRegistry.Types.VerticalArray, rootPos + step, isFixed: true);
                    var obj = AddNode(NodeTypeRegistry.Types.Object, rootPos + step * 3, isFixed: true);
                    obj.SetProperty("TypeName", "");
                    AddEdge(root.Guid, "Children", arr.Guid, "Parent", isFixed: true);
                    AddEdge(arr.Guid, "Children", obj.Guid, "Parent", isFixed: true);
                    break;
                }

                case GraphType.Object:
                {
                    var root = AddNode(NodeTypeRegistry.Types.Root, rootPos, isFixed: true);
                    var obj = AddNode(NodeTypeRegistry.Types.Object, rootPos + step, isFixed: true);
                    obj.SetProperty("TypeName", "");
                    AddEdge(root.Guid, "Children", obj.Guid, "Parent", isFixed: true);
                    break;
                }

                case GraphType.Enum:
                {
                    var root = AddNode(NodeTypeRegistry.Types.Root, rootPos, isFixed: true);
                    var enumNode = AddNode(NodeTypeRegistry.Types.Enum, rootPos + step, isFixed: true);
                    enumNode.SetProperty("TypeName", "");
                    enumNode.SetProperty("NameColumn", "A");
                    enumNode.SetProperty("ValueColumn", "B");
                    AddEdge(root.Guid, "Children", enumNode.Guid, "Parent", isFixed: true);
                    break;
                }

                case GraphType.Flag:
                {
                    var root = AddNode(NodeTypeRegistry.Types.Root, rootPos, isFixed: true);
                    var flagNode = AddNode(NodeTypeRegistry.Types.Flag, rootPos + step, isFixed: true);
                    flagNode.SetProperty("TypeName", "");
                    flagNode.SetProperty("NameColumn", "A");
                    flagNode.SetProperty("ValueColumn", "B");
                    AddEdge(root.Guid, "Children", flagNode.Guid, "Parent", isFixed: true);
                    break;
                }
            }
        }
    }

    internal enum GraphType { Dictionary, Array, Object, Enum, Flag }

    [Serializable]
    internal sealed class SerializedNode
    {
        [SerializeField] private string _guid = "";
        [SerializeField] private string _typeName = "";
        [SerializeField] private Vector2 _position;
        [SerializeField] private bool _isFixed;
        [SerializeField] private List<SerializedNodeProperty> _properties = new();

        public string Guid { get => _guid; set => _guid = value; }
        public string TypeName { get => _typeName; set => _typeName = value; }
        public Vector2 Position { get => _position; set => _position = value; }
        public bool IsFixed { get => _isFixed; set => _isFixed = value; }
        public List<SerializedNodeProperty> Properties => _properties;

        public string GetProperty(string key, string defaultValue = "")
        {
            foreach (var prop in _properties)
                if (prop.Key == key) return prop.Value;
            return defaultValue;
        }

        public void SetProperty(string key, string value)
        {
            foreach (var prop in _properties)
            {
                if (prop.Key == key) { prop.Value = value; return; }
            }
            _properties.Add(new SerializedNodeProperty { Key = key, Value = value });
        }
    }

    [Serializable]
    internal sealed class SerializedNodeProperty
    {
        [SerializeField] private string _key = "";
        [SerializeField] private string _value = "";
        public string Key { get => _key; set => _key = value; }
        public string Value { get => _value; set => _value = value; }
    }

    [Serializable]
    internal sealed class SerializedEdge
    {
        [SerializeField] private string _outputNodeGuid = "";
        [SerializeField] private string _outputPortName = "";
        [SerializeField] private string _inputNodeGuid = "";
        [SerializeField] private string _inputPortName = "";
        [SerializeField] private bool _isFixed;

        public string OutputNodeGuid { get => _outputNodeGuid; set => _outputNodeGuid = value; }
        public string OutputPortName { get => _outputPortName; set => _outputPortName = value; }
        public string InputNodeGuid { get => _inputNodeGuid; set => _inputNodeGuid = value; }
        public string InputPortName { get => _inputPortName; set => _inputPortName = value; }
        public bool IsFixed { get => _isFixed; set => _isFixed = value; }
    }
}
