using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.Commands;
using DataGraph.Editor.Nodes;
using DataGraph.Editor.Public;
using UnityEditor;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.UI
{
    /// <summary>
    /// Editor window for batch parsing DataGraph assets.
    /// Top half: graph list with format selection.
    /// Bottom half: integrated console with per-graph collapsible log groups.
    /// </summary>
    internal sealed class ParseRunnerPanel : EditorWindow
    {
        private List<GraphEntry> _graphEntries = new();
        private Vector2 _graphScrollPosition;
        private Vector2 _consoleScrollPosition;
        private string _outputPath = "Assets/DataGraph/Generated";
        private bool _isRunning;
        private float _splitRatio = 0.5f;
        private DataGraphConsole _console = new();
        private CancellationTokenSource _cts;

        [MenuItem("DataGraph/Parse Runner", false, 100)]
        private static void ShowWindow()
        {
            var window = GetWindow<ParseRunnerPanel>("DataGraph Parse Runner");
            window.minSize = new Vector2(450, 400);
        }

        private void OnEnable()
        {
            RefreshGraphList();
        }

        private void OnGUI()
        {
            var totalHeight = position.height;
            var topHeight = totalHeight * _splitRatio;
            var bottomHeight = totalHeight - topHeight;

            EditorGUILayout.BeginVertical();

            // --- Top section: graph list ---
            EditorGUILayout.BeginVertical(GUILayout.Height(topHeight));
            DrawHeader();
            DrawOutputPath();
            DrawAuthStatus();
            DrawGraphList();
            DrawActions();
            EditorGUILayout.EndVertical();

            // --- Resize handle ---
            var resizeRect = EditorGUILayout.GetControlRect(false, 4);
            resizeRect.height = 4;
            EditorGUI.DrawRect(resizeRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDrag && resizeRect.Contains(Event.current.mousePosition - new Vector2(0, 2)))
            {
                _splitRatio = Mathf.Clamp(Event.current.mousePosition.y / totalHeight, 0.2f, 0.8f);
                Repaint();
            }

            // --- Bottom section: console ---
            EditorGUILayout.BeginVertical(GUILayout.Height(bottomHeight));
            DrawConsole();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Parse Runner", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RefreshGraphList();
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
                EditorWindow.GetWindow<DataGraphSettingsPanel>("DataGraph Settings");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOutputPath()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output", GUILayout.Width(50));
            _outputPath = EditorGUILayout.TextField(_outputPath);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                var selected = EditorUtility.OpenFolderPanel("Select Output Folder", _outputPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    _outputPath = selected.StartsWith(Application.dataPath)
                        ? "Assets" + selected.Substring(Application.dataPath.Length)
                        : selected;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAuthStatus()
        {
            if (!ProviderRegistry.IsGoogleSheetsAvailable())
            {
                EditorGUILayout.HelpBox("Google Sheets provider not installed.", MessageType.Warning);
                return;
            }

            var provider = ProviderRegistry.CreateGoogleSheetsProvider();
            var color = provider.IsAuthenticated
                ? new Color(0.1f, 0.7f, 0.4f)
                : new Color(0.8f, 0.3f, 0.3f);
            var label = provider.IsAuthenticated
                ? "Google Sheets: Connected"
                : "Google Sheets: Not authenticated";

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var dot = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
            dot.y += 3;
            EditorGUI.DrawRect(new Rect(dot.x, dot.y, 8, 8), color);
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphList()
        {
            if (_graphEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No .datagraph files found. Create one via Assets > Create > DataGraph > Parser Graph.",
                    MessageType.Info);
                return;
            }

            // Column header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Graph", EditorStyles.miniLabel, GUILayout.MinWidth(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("SO", EditorStyles.miniLabel, GUILayout.Width(28));
            EditorGUILayout.LabelField("JSON", EditorStyles.miniLabel, GUILayout.Width(36));
            EditorGUILayout.EndHorizontal();

            _graphScrollPosition = EditorGUILayout.BeginScrollView(_graphScrollPosition);

            foreach (var entry in _graphEntries)
            {
                EditorGUILayout.BeginHorizontal();
                entry.Selected = EditorGUILayout.ToggleLeft(entry.DisplayName, entry.Selected, GUILayout.MinWidth(100));
                GUILayout.FlexibleSpace();
                entry.GenerateSO = EditorGUILayout.Toggle(entry.GenerateSO, GUILayout.Width(28));
                entry.GenerateJSON = EditorGUILayout.Toggle(entry.GenerateJSON, GUILayout.Width(36));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActions()
        {
            var selectedCount = _graphEntries.Count(e => e.Selected);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                _graphEntries.ForEach(e => e.Selected = true);
            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                _graphEntries.ForEach(e => e.Selected = false);

            GUILayout.FlexibleSpace();

            if (_isRunning)
            {
                if (GUILayout.Button("Cancel", GUILayout.Width(60)))
                    _cts?.Cancel();
                EditorGUILayout.LabelField("Parsing...", EditorStyles.miniLabel, GUILayout.Width(60));
            }
            else
            {
                EditorGUI.BeginDisabledGroup(selectedCount == 0);
                if (GUILayout.Button($"Parse selected ({selectedCount})", GUILayout.Width(140)))
                    RunParseAsync();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ==================== CONSOLE ====================

        private void DrawConsole()
        {
            // Console toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Console", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.FlexibleSpace();

            DrawSeverityBadge(new Color(0.9f, 0.3f, 0.3f), _console.TotalErrors);
            DrawSeverityBadge(new Color(0.9f, 0.7f, 0.2f), _console.TotalWarnings);
            DrawSeverityBadge(new Color(0.3f, 0.6f, 0.9f), _console.TotalInfos);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(40)))
                _console.Clear();
            EditorGUILayout.EndHorizontal();

            if (_console.Groups.Count == 0)
            {
                EditorGUILayout.HelpBox("Run a parse to see results here.", MessageType.None);
                return;
            }

            // Console content — collapsible groups
            _consoleScrollPosition = EditorGUILayout.BeginScrollView(_consoleScrollPosition);

            foreach (var group in _console.Groups)
            {
                DrawLogGroup(group);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLogGroup(GraphLogGroup group)
        {
            // Group header
            var headerStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = group.Success ? FontStyle.Normal : FontStyle.Bold
            };

            var statusIcon = !group.IsComplete ? "\u25cb"
                : group.Success ? "\u2713"
                : "\u2717";

            var statusColor = !group.IsComplete ? Color.gray
                : group.Success ? new Color(0.1f, 0.7f, 0.4f)
                : new Color(0.9f, 0.3f, 0.3f);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var prevColor = GUI.contentColor;
            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField(statusIcon, GUILayout.Width(16));
            GUI.contentColor = prevColor;

            group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.GraphName, true, headerStyle);

            GUILayout.FlexibleSpace();

            if (group.ErrorCount > 0)
                DrawMiniCount(new Color(0.9f, 0.3f, 0.3f), group.ErrorCount);
            if (group.WarningCount > 0)
                DrawMiniCount(new Color(0.9f, 0.7f, 0.2f), group.WarningCount);

            if (group.IsComplete)
            {
                var elapsed = group.EndTime - group.StartTime;
                EditorGUILayout.LabelField(
                    $"{elapsed.TotalSeconds:F1}s",
                    EditorStyles.miniLabel, GUILayout.Width(35));
            }

            EditorGUILayout.EndHorizontal();

            // Group entries
            if (!group.IsExpanded)
                return;

            EditorGUI.indentLevel++;
            foreach (var entry in group.Entries)
            {
                DrawLogEntry(entry);
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawLogEntry(ConsoleLogEntry entry)
        {
            var color = entry.Severity switch
            {
                LogSeverity.Error => new Color(0.9f, 0.3f, 0.3f),
                LogSeverity.Warning => new Color(0.85f, 0.65f, 0.1f),
                LogSeverity.Success => new Color(0.1f, 0.7f, 0.4f),
                _ => Color.gray
            };

            var label = entry.Severity switch
            {
                LogSeverity.Error => "[Error]",
                LogSeverity.Warning => "[Warn]",
                LogSeverity.Success => "[Done]",
                _ => "[Info]"
            };

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.contentColor;

            EditorGUILayout.LabelField(
                entry.Timestamp.ToString("HH:mm:ss"),
                EditorStyles.miniLabel, GUILayout.Width(55));

            GUI.contentColor = color;
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(42));
            GUI.contentColor = prevColor;

            EditorGUILayout.LabelField(entry.Message, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSeverityBadge(Color color, int count)
        {
            var rect = GUILayoutUtility.GetRect(20, 14, GUILayout.Width(20));
            rect.y += 2;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 8, 8), color);
            var labelRect = new Rect(rect.x + 10, rect.y - 2, 20, 14);
            GUI.Label(labelRect, count.ToString(), EditorStyles.miniLabel);
        }

        private static void DrawMiniCount(Color color, int count)
        {
            var rect = GUILayoutUtility.GetRect(18, 14, GUILayout.Width(18));
            rect.y += 2;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 6, 6), color);
            var labelRect = new Rect(rect.x + 8, rect.y - 2, 16, 14);
            GUI.Label(labelRect, count.ToString(), EditorStyles.miniLabel);
        }

        // ==================== PARSE EXECUTION ====================

        private async void RunParseAsync()
        {
            _isRunning = true;
            _console.Clear();
            _cts = new CancellationTokenSource();
            Repaint();

            try
            {
                var provider = ResolveProvider();
                if (provider == null)
                {
                    var errorGroup = _console.BeginGroup("(all)");
                    errorGroup.LogError("No data source provider available. Configure in DataGraph > Settings.");
                    errorGroup.Complete(false);
                    return;
                }

                var command = new ParseGraphCommand();
                var selected = _graphEntries.Where(e => e.Selected).ToList();
                var tasks = new List<Task>();

                foreach (var entry in selected)
                {
                    if (_cts.Token.IsCancellationRequested)
                        break;

                    var log = _console.BeginGroup(entry.DisplayName);
                    var formats = new ParseGraphCommand.FormatSelection
                    {
                        GenerateSO = entry.GenerateSO,
                        GenerateJSON = entry.GenerateJSON
                    };

                    var task = command.ExecuteAsync(
                        entry.GraphAsset, provider, formats,
                        _outputPath, log, _cts.Token);
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                var errorGroup = _console.BeginGroup("(error)");
                errorGroup.LogError(ex.Message);
                errorGroup.Complete(false);
            }
            finally
            {
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
                Repaint();
            }
        }

        private void RefreshGraphList()
        {
            _graphEntries.Clear();

            var guids = AssetDatabase.FindAssets("");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".datagraph"))
                    continue;

                var graph = GraphDatabase.LoadGraph<DataGraphAsset>(path);
                if (graph == null)
                    continue;

                _graphEntries.Add(new GraphEntry
                {
                    AssetPath = path,
                    GraphAsset = graph,
                    DisplayName = !string.IsNullOrEmpty(graph.GraphName)
                        ? graph.GraphName
                        : Path.GetFileNameWithoutExtension(path),
                    Selected = false,
                    GenerateSO = true,
                    GenerateJSON = true
                });
            }

            Repaint();
        }

        private static ISheetProvider ResolveProvider()
        {
            if (ProviderRegistry.IsGoogleSheetsAvailable())
                return ProviderRegistry.CreateGoogleSheetsProvider();
            return null;
        }

        private class GraphEntry
        {
            public string AssetPath;
            public DataGraphAsset GraphAsset;
            public string DisplayName;
            public bool Selected;
            public bool GenerateSO;
            public bool GenerateJSON;
        }
    }
}
