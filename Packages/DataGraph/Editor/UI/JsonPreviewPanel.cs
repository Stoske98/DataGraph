using System;
using System.Threading;
using DataGraph.Editor.Commands;
using DataGraph.Editor.Nodes;
using DataGraph.Editor.Public;
using DataGraph.Editor.Serialization;
using DataGraph.Runtime;
using UnityEditor;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.UI
{
    /// <summary>
    /// Editor window showing live JSON preview of parsed data.
    /// Fetches once and caches. Re-parses from cache on graph changes
    /// with debounce. Explicit Refresh triggers new fetch.
    /// </summary>
    internal sealed class JsonPreviewPanel : EditorWindow
    {
        private DataGraphAsset _targetGraph;
        private RawTableData _cachedData;
        private string _jsonPreview = "";
        private string _statusMessage = "";
        private bool _isInvalidState;
        private bool _isFetching;
        private bool _fullPreview;
        private Vector2 _scrollPosition;
        private double _lastChangeTime;
        private bool _pendingReparse;
        private CancellationTokenSource _fetchCts;
        private string _cacheTimestamp = "";

        private const double DebounceDelay = 0.6;

        [MenuItem("DataGraph/JSON Preview", false, 101)]
        private static void ShowWindow()
        {
            var window = GetWindow<JsonPreviewPanel>("JSON Preview");
            window.minSize = new Vector2(300, 200);
        }

        public static void ShowForGraph(DataGraphAsset graph)
        {
            var window = GetWindow<JsonPreviewPanel>("JSON Preview");
            window._targetGraph = graph;
            window._cachedData = null;
            window._jsonPreview = "";
            window.FetchAndParse();
        }

        private void LoadGraphFromPath(string path)
        {
            try
            {
                var graph = GraphDatabase.LoadGraph<DataGraphAsset>(path);
                if (graph != null)
                {
                    _targetGraph = graph;
                    _cachedData = null;
                    _jsonPreview = "";
                    _statusMessage = "";
                    FetchAndParse();
                }
            }
            catch
            {
                _statusMessage = "Failed to load graph from path.";
            }
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _fetchCts?.Cancel();
            _fetchCts?.Dispose();
        }

        private void OnEditorUpdate()
        {
            if (!_pendingReparse) return;
            if (EditorApplication.timeSinceStartup - _lastChangeTime < DebounceDelay) return;

            _pendingReparse = false;
            ReparseFromCache();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_targetGraph == null)
            {
                EditorGUILayout.HelpBox(
                    "No graph selected. Open a .datagraph file or select one above.",
                    MessageType.Info);
                return;
            }

            if (_isFetching)
            {
                EditorGUILayout.HelpBox("Fetching data from sheet...", MessageType.None);
            }

            if (_isInvalidState)
            {
                EditorGUILayout.HelpBox(
                    "[!] Graph is invalid. Showing last valid preview.", MessageType.Warning);
            }

            DrawJsonContent();
            DrawStatusBar();
        }

        private string _graphPath = "";

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _graphPath = EditorGUILayout.TextField(_graphPath, GUILayout.MinWidth(120));
            if (EditorGUI.EndChangeCheck() && _graphPath.EndsWith(".datagraph"))
            {
                LoadGraphFromPath(_graphPath);
            }

            GUILayout.FlexibleSpace();

            _fullPreview = GUILayout.Toggle(_fullPreview, "Full",
                EditorStyles.toolbarButton, GUILayout.Width(36));

            EditorGUI.BeginDisabledGroup(_isFetching || _targetGraph == null);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55)))
                FetchAndParse();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawJsonContent()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var style = EditorStyles.textArea;
            style.wordWrap = false;

            EditorGUILayout.TextArea(_jsonPreview, style,
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var color = _isInvalidState
                    ? new Color(0.9f, 0.3f, 0.3f)
                    : new Color(0.1f, 0.7f, 0.4f);
                var prevColor = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.LabelField(
                    _isInvalidState ? "Invalid" : "Valid",
                    EditorStyles.miniLabel, GUILayout.Width(42));
                GUI.contentColor = prevColor;
            }

            EditorGUILayout.LabelField(_statusMessage, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_cacheTimestamp))
            {
                EditorGUILayout.LabelField(
                    $"Cached: {_cacheTimestamp}",
                    EditorStyles.miniLabel, GUILayout.Width(100));
            }

            EditorGUILayout.EndHorizontal();
        }

        public void OnGraphChanged()
        {
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingReparse = true;
        }

        private async void FetchAndParse()
        {
            if (_targetGraph == null) return;

            _isFetching = true;
            _statusMessage = "Fetching...";
            Repaint();

            _fetchCts?.Cancel();
            _fetchCts?.Dispose();
            _fetchCts = new CancellationTokenSource();

            try
            {
                var provider = ResolveProvider();
                if (provider == null)
                {
                    _statusMessage = "No provider available";
                    _isFetching = false;
                    Repaint();
                    return;
                }

                var sheetRef = new SheetReference(
                    _targetGraph.SheetId, _targetGraph.HeaderRowOffset, _targetGraph.SheetName);

                var result = await provider.FetchAsync(sheetRef, _fetchCts.Token);
                if (result.IsFailure)
                {
                    _statusMessage = result.Error;
                    _isFetching = false;
                    Repaint();
                    return;
                }

                _cachedData = result.Value;
                _cacheTimestamp = DateTime.Now.ToString("HH:mm:ss");
                _isFetching = false;
                ReparseFromCache();
            }
            catch (OperationCanceledException)
            {
                _isFetching = false;
            }
            catch (Exception ex)
            {
                _statusMessage = ex.Message;
                _isFetching = false;
            }

            Repaint();
        }

        private void ReparseFromCache()
        {
            if (_targetGraph == null || _cachedData == null) return;

            var command = new ParseGraphCommand();
            int maxEntries = _fullPreview ? 0 : 1;
            var parseResult = command.ParseFromCache(_targetGraph, _cachedData, maxEntries);

            if (parseResult.IsFailure)
            {
                _isInvalidState = true;
                _statusMessage = parseResult.Error;
                Repaint();
                return;
            }

            _isInvalidState = false;

            var serializer = new JsonDataSerializer(prettyPrint: true);
            var jsonResult = serializer.Serialize(parseResult.Value);

            if (jsonResult.IsSuccess)
            {
                _jsonPreview = jsonResult.Value;
                _statusMessage = _fullPreview ? "Full preview" : "First element preview";
            }
            else
            {
                _statusMessage = jsonResult.Error;
            }

            Repaint();
        }

        private static ISheetProvider ResolveProvider()
        {
            if (ProviderRegistry.IsGoogleSheetsAvailable())
                return ProviderRegistry.CreateGoogleSheetsProvider();
            return null;
        }
    }
}
