using System;
using System.Linq;
using System.Reflection;
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
    /// Automatically detects the active graph from the GTK editor.
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
        private double _lastGraphCheck;
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
            if (_pendingReparse)
            {
                if (EditorApplication.timeSinceStartup - _lastChangeTime >= DebounceDelay)
                {
                    _pendingReparse = false;
                    ReparseFromCache();
                }
            }

            if (EditorApplication.timeSinceStartup - _lastGraphCheck > 0.5)
            {
                _lastGraphCheck = EditorApplication.timeSinceStartup;
                DetectActiveGraph();
            }
        }

        /// <summary>
        /// Finds the active DataGraphAsset from the focused GTK editor window.
        /// Path: Window.GraphTool.ToolState.GraphModel.Graph
        /// </summary>
        private void DetectActiveGraph()
        {
            var focused = EditorWindow.focusedWindow;
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();

            foreach (var window in windows)
            {
                var windowType = window.GetType();
                if (!windowType.FullName.Contains("GraphViewEditorWindow")) continue;

                if (focused != null && focused != window && windows.Length > 1
                    && focused.GetType().FullName.Contains("GraphViewEditorWindow"))
                    continue;

                try
                {
                    var tool = windowType.GetProperty("GraphTool",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(window);
                    if (tool == null) continue;

                    var toolState = tool.GetType().GetProperty("ToolState",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(tool);
                    if (toolState == null) continue;

                    var graphModel = toolState.GetType().GetProperty("GraphModel",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(toolState);
                    if (graphModel == null) continue;

                    var graph = graphModel.GetType().GetProperty("Graph",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(graphModel);

                    if (graph is DataGraphAsset dga && dga != _targetGraph)
                    {
                        _targetGraph = dga;
                        _cachedData = null;
                        _jsonPreview = "";
                        _statusMessage = "";
                        FetchAndParse();
                    }
                    return;
                }
                catch
                {
                    continue;
                }
            }
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

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var graphLabel = _targetGraph != null
                ? (!string.IsNullOrEmpty(_targetGraph.GraphName) ? _targetGraph.GraphName : "Unnamed")
                : "No graph";
            EditorGUILayout.LabelField(graphLabel, EditorStyles.miniLabel, GUILayout.MinWidth(80));

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

        private ISheetProvider ResolveProvider()
        {
            if (_targetGraph == null)
                return null;

            var sheetId = _targetGraph.SheetId;

            if (!string.IsNullOrEmpty(sheetId))
            {
                if (IsLocalFilePath(sheetId))
                {
                    if (ProviderRegistry.IsLocalFileAvailable())
                        return ProviderRegistry.CreateLocalFileProvider();
                }
                else
                {
                    if (ProviderRegistry.IsGoogleSheetsAvailable())
                    {
                        var gs = ProviderRegistry.CreateGoogleSheetsProvider();
                        if (gs.IsAuthenticated) return gs;
                    }
                }
            }

            if (ProviderRegistry.IsGoogleSheetsAvailable())
            {
                var gs = ProviderRegistry.CreateGoogleSheetsProvider();
                if (gs.IsAuthenticated) return gs;
            }

            if (ProviderRegistry.IsLocalFileAvailable())
                return ProviderRegistry.CreateLocalFileProvider();

            return null;
        }

        private static bool IsLocalFilePath(string sheetId)
        {
            if (sheetId.StartsWith("Assets/") || sheetId.StartsWith("Assets\\"))
                return true;
            var ext = System.IO.Path.GetExtension(sheetId)?.ToLowerInvariant();
            return ext == ".csv" || ext == ".tsv" || ext == ".xlsx";
        }
    }
}
