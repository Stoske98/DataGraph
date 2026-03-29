using System;
using System.Collections.Generic;
using UnityEngine;

namespace DataGraph.Editor
{
    /// <summary>
    /// ScriptableObject that stores a complete DataGraph definition.
    /// Replaces the GTK Graph-based DataGraphAsset with custom serialization.
    /// Property names match the old API for downstream compatibility.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGraph", menuName = "DataGraph/Graph")]
    internal sealed class DataGraphAsset : ScriptableObject
    {
        [SerializeField] private string _graphName = "";
        [SerializeField] private string _sheetId = "";
        [SerializeField] private string _sheetName = "Sheet1";
        [SerializeField] private int _headerRowOffset = 1;
        [SerializeField] private GraphType _graphType = GraphType.Data;
        [SerializeField] private List<SerializedNode> _nodes = new();
        [SerializeField] private List<SerializedEdge> _edges = new();
        [SerializeField] private List<string> _cachedHeaders = new();
        [SerializeField] private long _lastFetchTimeTicks;

        public string GraphName
        {
            get => _graphName;
            set => _graphName = value;
        }

        public string SheetId
        {
            get => _sheetId;
            set => _sheetId = value;
        }

        public string SheetName
        {
            get => _sheetName;
            set => _sheetName = value;
        }

        public int HeaderRowOffset
        {
            get => _headerRowOffset;
            set => _headerRowOffset = value;
        }

        public GraphType GraphType
        {
            get => _graphType;
            set => _graphType = value;
        }

        public List<SerializedNode> Nodes => _nodes;
        public List<SerializedEdge> Edges => _edges;
        public IReadOnlyList<string> CachedHeaders => _cachedHeaders;
        public DateTime LastFetchTime => new DateTime(_lastFetchTimeTicks);

        /// <summary>
        /// Updates cached column headers from a fetched sheet.
        /// </summary>
        public void UpdateCachedHeaders(IReadOnlyList<string> headers)
        {
            _cachedHeaders.Clear();
            if (headers != null)
                foreach (var h in headers)
                    _cachedHeaders.Add(h);
            _lastFetchTimeTicks = DateTime.Now.Ticks;
        }

        /// <summary>
        /// Returns column choices for dropdown population.
        /// Uses cached headers if available, falls back to A-Z.
        /// </summary>
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
                if (node.Guid == guid)
                    return node;
            return null;
        }

        public SerializedNode AddNode(string typeName, Vector2 position)
        {
            var node = new SerializedNode
            {
                Guid = System.Guid.NewGuid().ToString(),
                TypeName = typeName,
                Position = position
            };
            _nodes.Add(node);
            return node;
        }

        public void RemoveNode(string guid)
        {
            _nodes.RemoveAll(n => n.Guid == guid);
            _edges.RemoveAll(e => e.OutputNodeGuid == guid || e.InputNodeGuid == guid);
        }

        public SerializedEdge AddEdge(string outputNodeGuid, string outputPortName,
            string inputNodeGuid, string inputPortName)
        {
            var edge = new SerializedEdge
            {
                OutputNodeGuid = outputNodeGuid,
                OutputPortName = outputPortName,
                InputNodeGuid = inputNodeGuid,
                InputPortName = inputPortName
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
                e.InputPortName == inputPortName);
        }

        public List<SerializedEdge> GetChildEdges(string parentNodeGuid)
        {
            var result = new List<SerializedEdge>();
            foreach (var edge in _edges)
                if (edge.OutputNodeGuid == parentNodeGuid)
                    result.Add(edge);
            return result;
        }
    }

    internal enum GraphType { Data, Enum, Flag }

    [Serializable]
    internal sealed class SerializedNode
    {
        [SerializeField] private string _guid = "";
        [SerializeField] private string _typeName = "";
        [SerializeField] private Vector2 _position;
        [SerializeField] private List<SerializedNodeProperty> _properties = new();

        public string Guid { get => _guid; set => _guid = value; }
        public string TypeName { get => _typeName; set => _typeName = value; }
        public Vector2 Position { get => _position; set => _position = value; }
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

        public string OutputNodeGuid { get => _outputNodeGuid; set => _outputNodeGuid = value; }
        public string OutputPortName { get => _outputPortName; set => _outputPortName = value; }
        public string InputNodeGuid { get => _inputNodeGuid; set => _inputNodeGuid = value; }
        public string InputPortName { get => _inputPortName; set => _inputPortName = value; }
    }
}
