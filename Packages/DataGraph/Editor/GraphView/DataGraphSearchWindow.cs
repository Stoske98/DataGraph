using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DataGraph.Editor.GraphView
{
    /// <summary>
    /// Search window for adding nodes to the DataGraph editor.
    /// Nodes are categorized into Roots, Fields, and Leaves.
    /// </summary>
    internal sealed class DataGraphSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private DataGraphView _graphView;
        private EditorWindow _window;

        /// <summary>
        /// Local graph position where the node should be created.
        /// Set by DataGraphView before opening the search window.
        /// </summary>
        public Vector2 CreationPosition { get; set; }

        public void Initialize(DataGraphView graphView, EditorWindow window)
        {
            _graphView = graphView;
            _window = window;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"), 0)
            };

            var entries = NodeTypeRegistry.GetSearchEntries();
            string lastCategory = null;

            foreach (var (category, typeName, displayName) in entries)
            {
                if (category != lastCategory)
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(category), 1));
                    lastCategory = category;
                }

                tree.Add(new SearchTreeEntry(new GUIContent("  " + displayName))
                {
                    userData = typeName,
                    level = 2
                });
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var typeName = (string)entry.userData;
            _graphView.CreateNode(typeName, CreationPosition);
            return true;
        }
    }
}
