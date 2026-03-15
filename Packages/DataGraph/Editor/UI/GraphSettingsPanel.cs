using DataGraph.Editor.Nodes;
using UnityEditor;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.UI
{
    /// <summary>
    /// Small editor window for configuring graph-level properties
    /// (Sheet ID, Header Row Offset, Graph Name) on a .datagraph asset.
    /// </summary>
    internal sealed class GraphSettingsPanel : EditorWindow
    {
        private string _graphPath = "";
        private DataGraphAsset _graph;
        private string _sheetId = "";
        private string _sheetName = "Sheet1";
        private int _headerRowOffset = 1;
        private string _graphName = "";
        private string _statusMessage = "";

        [MenuItem("DataGraph/Graph Settings", false, 102)]
        private static void ShowWindow()
        {
            var window = GetWindow<GraphSettingsPanel>("Graph Settings");
            window.minSize = new Vector2(380, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Graph Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _graphPath = EditorGUILayout.TextField("Graph Path", _graphPath);
            if (EditorGUI.EndChangeCheck())
            {
                LoadGraph();
            }

            EditorGUILayout.Space(4);

            if (_graph == null)
            {
                EditorGUILayout.HelpBox(
                    "Enter the path to a .datagraph file (e.g. Assets/DataGraph/Items.datagraph)",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Loaded", _graphPath, EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            _graphName = EditorGUILayout.TextField("Graph Name", _graphName);
            _sheetId = EditorGUILayout.TextField("Sheet ID / URL", _sheetId);
            _sheetName = EditorGUILayout.TextField("Sheet Tab Name", _sheetName);
            _headerRowOffset = EditorGUILayout.IntField("Header Row Offset", _headerRowOffset);

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Save", GUILayout.Height(25)))
            {
                SaveSettings();
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }

        private void LoadGraph()
        {
            _graph = null;
            _statusMessage = "";

            if (string.IsNullOrEmpty(_graphPath) || !_graphPath.EndsWith(".datagraph"))
                return;

            try
            {
                _graph = GraphDatabase.LoadGraph<DataGraphAsset>(_graphPath);
                if (_graph != null)
                {
                    _sheetId = _graph.SheetId ?? "";
                    _sheetName = _graph.SheetName ?? "Sheet1";
                    _headerRowOffset = _graph.HeaderRowOffset;
                    _graphName = _graph.GraphName ?? "";
                }
            }
            catch
            {
                _statusMessage = "Failed to load graph.";
            }
        }

        private void SaveSettings()
        {
            if (_graph == null)
                return;

            _graph.GraphName = _graphName;
            _graph.SheetId = _sheetId;
            _graph.SheetName = _sheetName;
            _graph.HeaderRowOffset = _headerRowOffset;
            GraphDatabase.SaveGraphIfDirty(_graph);
            _statusMessage = "Saved.";
        }
    }
}
