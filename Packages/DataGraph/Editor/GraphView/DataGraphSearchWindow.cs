using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DataGraph.Editor.GraphView
{
    /// <summary>
    /// Search window for adding nodes. Root/Enum/Flag excluded (auto-created).
    /// </summary>
    internal sealed class DataGraphSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private DataGraphView _graphView;
        private EditorWindow _window;
        public Vector2 CreationPosition { get; set; }

        public void Initialize(DataGraphView graphView, EditorWindow window)
        {
            _graphView = graphView;
            _window = window;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Create Node"), 0) };
            string lastCat = null;
            foreach (var (cat, typeName, displayName) in NodeTypeRegistry.GetSearchEntries())
            {
                if (cat != lastCat) { tree.Add(new SearchTreeGroupEntry(new GUIContent(cat), 1)); lastCat = cat; }
                tree.Add(new SearchTreeEntry(new GUIContent("  " + displayName)) { userData = typeName, level = 2 });
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            _graphView.CreateNode((string)entry.userData, CreationPosition);
            return true;
        }
    }
}
