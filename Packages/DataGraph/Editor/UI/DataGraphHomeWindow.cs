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
    /// Main DataGraph editor window. Serves as the home screen for managing
    /// graphs, running parses, and configuring settings. Double-click a
    /// graph to open the GTK graph editor.
    /// </summary>
    internal sealed class DataGraphHomeWindow : EditorWindow
    {
        private enum Screen { Setup, Home }

        private Screen _currentScreen = Screen.Home;
        private List<GraphEntry> _graphEntries = new();
        private Vector2 _graphScrollPosition;
        private Vector2 _consoleScrollPosition;
        private string _outputPath = "Assets/DataGraph/Generated";
        private string _graphsPath = "Assets/DataGraph/Graphs";
        private bool _isRunning;
        private float _splitRatio = 0.55f;
        private DataGraphConsole _console = new();
        private CancellationTokenSource _cts;
        private bool _showSettings;

        private int _authMethodIndex;
        private string _apiKey = "";
        private string _oauthClientId = "";
        private string _oauthClientSecret = "";
        private string _authStatus = "";
        private MessageType _authStatusType = MessageType.None;

        private int _editingGraphIndex = -1;
        private string _editSheetId = "";
        private string _editSheetName = "";
        private int _editHeaderOffset = 1;
        private string _editGraphName = "";

        [MenuItem("DataGraph/Home", false, 0)]
        private static void ShowWindow()
        {
            var window = GetWindow<DataGraphHomeWindow>("DataGraph");
            window.minSize = new Vector2(550, 400);
        }

        private void OnEnable()
        {
            LoadAuthSettings();
            if (!IsProviderConfigured())
                _currentScreen = Screen.Setup;
            else
                RefreshGraphList();

            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnFocus()
        {
            if (_currentScreen == Screen.Home)
                RefreshGraphList();
        }

        private void OnProjectChanged()
        {
            if (_currentScreen == Screen.Home)
                RefreshGraphList();
        }

        private void OnGUI()
        {
            switch (_currentScreen)
            {
                case Screen.Setup:
                    DrawSetupScreen();
                    break;
                case Screen.Home:
                    DrawHomeScreen();
                    break;
            }
        }

        // ==================== SETUP SCREEN ====================

        private void DrawSetupScreen()
        {
            EditorGUILayout.Space(40);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(380));

            EditorGUILayout.LabelField("DataGraph", new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30));
            EditorGUILayout.LabelField("Connect a data source to get started", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("Google Sheets", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _authMethodIndex = GUILayout.Toolbar(_authMethodIndex, new[] { "API Key", "OAuth 2.0" });
            EditorGUILayout.Space(8);

            if (_authMethodIndex == 0)
            {
                _apiKey = EditorGUILayout.TextField("API Key", _apiKey);
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Connect", GUILayout.Height(28)))
                    SaveApiKeyAndContinue();
            }
            else
            {
                _oauthClientId = EditorGUILayout.TextField("Client ID", _oauthClientId);
                _oauthClientSecret = EditorGUILayout.PasswordField("Client Secret", _oauthClientSecret);
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Sign In with Google", GUILayout.Height(28)))
                    SignInOAuthAndContinue();
            }

            if (!string.IsNullOrEmpty(_authStatus))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_authStatus, _authStatusType);
            }

            EditorGUILayout.Space(20);

            if (ProviderRegistry.IsLocalFileAvailable())
            {
                if (GUILayout.Button("Local File (CSV / Excel)", GUILayout.Height(28)))
                {
                    _currentScreen = Screen.Home;
                    RefreshGraphList();
                }
                EditorGUILayout.LabelField("No setup needed — just set file path on each graph.",
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("Local File (CSV / Excel) — not installed", GUILayout.Height(28));
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(8);

            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("OneDrive — Coming soon", GUILayout.Height(28));
            EditorGUI.EndDisabledGroup();

            if (IsProviderConfigured())
            {
                EditorGUILayout.Space(12);
                if (GUILayout.Button("Skip to Home", GUILayout.Height(24)))
                {
                    _currentScreen = Screen.Home;
                    RefreshGraphList();
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ==================== HOME SCREEN ====================

        private void DrawHomeScreen()
        {
            DrawHomeToolbar();

            if (_showSettings)
            {
                DrawSettingsPopup();
                return;
            }

            var totalHeight = position.height - 52;
            var topHeight = totalHeight * _splitRatio;

            EditorGUILayout.BeginVertical(GUILayout.Height(topHeight));
            DrawGraphList();
            DrawActionBar();
            EditorGUILayout.EndVertical();

            DrawResizeHandle();

            EditorGUILayout.BeginVertical();
            DrawConsole();
            EditorGUILayout.EndVertical();
        }

        private void DrawHomeToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("DataGraph", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.FlexibleSpace();

            var authColor = IsProviderConfigured()
                ? new Color(0.1f, 0.7f, 0.4f)
                : new Color(0.8f, 0.3f, 0.3f);
            var authLabel = IsProviderConfigured()
                ? "Google Sheets"
                : "Not connected";

            var dot = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
            dot.y += 5;
            EditorGUI.DrawRect(new Rect(dot.x, dot.y, 8, 8), authColor);
            EditorGUILayout.LabelField(authLabel, EditorStyles.miniLabel, GUILayout.Width(90));

            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(55)))
                _showSettings = !_showSettings;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionBar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(4);

            if (GUILayout.Button("+ New Graph", GUILayout.Width(90), GUILayout.Height(22)))
                CreateNewGraph();

            GUILayout.FlexibleSpace();

            var selectedCount = _graphEntries.Count(e => e.Selected);

            if (_isRunning)
            {
                if (GUILayout.Button("Cancel", GUILayout.Width(55), GUILayout.Height(22)))
                    _cts?.Cancel();
            }
            else
            {
                EditorGUI.BeginDisabledGroup(selectedCount == 0);
                if (GUILayout.Button($"Parse ({selectedCount})", GUILayout.Width(85), GUILayout.Height(22)))
                    RunParseAsync();
                if (GUILayout.Button($"Create Assets ({selectedCount})", GUILayout.Width(120), GUILayout.Height(22)))
                    RunCreateAssetsAsync();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawGraphList()
        {
            if (_graphEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No .datagraph files found.\nClick '+ New Graph' to create one.",
                    MessageType.Info);
                return;
            }

            var blobAvailable = ProviderRegistry.IsBlobAvailable();
            var quantumAvailable = ProviderRegistry.IsQuantumAvailable();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField("Graph", EditorStyles.miniLabel, GUILayout.MinWidth(80));
            EditorGUILayout.LabelField("Sheet Tab", EditorStyles.miniLabel, GUILayout.Width(65));
            EditorGUILayout.LabelField("SO", EditorStyles.miniLabel, GUILayout.Width(25));
            EditorGUILayout.LabelField("JSON", EditorStyles.miniLabel, GUILayout.Width(32));
            if (blobAvailable)
                EditorGUILayout.LabelField("Blob", EditorStyles.miniLabel, GUILayout.Width(30));
            if (quantumAvailable)
                EditorGUILayout.LabelField("QSO", EditorStyles.miniLabel, GUILayout.Width(30));
            EditorGUILayout.LabelField("", GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();

            _graphScrollPosition = EditorGUILayout.BeginScrollView(_graphScrollPosition);

            for (int i = 0; i < _graphEntries.Count; i++)
            {
                var entry = _graphEntries[i];

                if (_editingGraphIndex == i)
                {
                    DrawGraphEditRow(entry);
                    continue;
                }

                var rect = EditorGUILayout.BeginHorizontal();

                if (Event.current.type == EventType.MouseDown &&
                    Event.current.clickCount == 2 &&
                    rect.Contains(Event.current.mousePosition))
                {
                    OpenGraphEditor(entry);
                    Event.current.Use();
                }

                entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(20));
                EditorGUILayout.LabelField(entry.DisplayName, GUILayout.MinWidth(80));
                EditorGUILayout.LabelField(entry.GraphAsset.SheetName, EditorStyles.miniLabel, GUILayout.Width(65));
                entry.GenerateSO = EditorGUILayout.Toggle(entry.GenerateSO, GUILayout.Width(25));
                entry.GenerateJSON = EditorGUILayout.Toggle(entry.GenerateJSON, GUILayout.Width(32));
                if (blobAvailable)
                    entry.GenerateBlob = EditorGUILayout.Toggle(entry.GenerateBlob, GUILayout.Width(30));
                if (quantumAvailable)
                    entry.GenerateQuantum = EditorGUILayout.Toggle(entry.GenerateQuantum, GUILayout.Width(30));

                if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(25)))
                {
                    _editingGraphIndex = i;
                    _editGraphName = entry.GraphAsset.GraphName ?? "";
                    _editSheetId = entry.GraphAsset.SheetId ?? "";
                    _editSheetName = entry.GraphAsset.SheetName ?? "Sheet1";
                    _editHeaderOffset = entry.GraphAsset.HeaderRowOffset;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGraphEditRow(GraphEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Edit: {entry.DisplayName}", EditorStyles.boldLabel);
            _editGraphName = EditorGUILayout.TextField("Graph Name", _editGraphName);

            EditorGUILayout.BeginHorizontal();
            _editSheetId = EditorGUILayout.TextField("Data Source", _editSheetId);
            if (GUILayout.Button("File", EditorStyles.miniButton, GUILayout.Width(35)))
            {
                var path = EditorUtility.OpenFilePanel(
                    "Select Data File", Application.dataPath, "csv,xlsx,tsv");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _editSheetId = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _editSheetId = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            _editSheetName = EditorGUILayout.TextField("Sheet Tab", _editSheetName);
            _editHeaderOffset = EditorGUILayout.IntField("Header Offset", _editHeaderOffset);

            EditorGUILayout.BeginHorizontal();

            bool shouldDelete = false;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog(
                    "Delete Graph",
                    $"Delete '{entry.DisplayName}' and all generated files?\n\nThis cannot be undone.",
                    "Delete", "Cancel"))
                {
                    shouldDelete = true;
                }
            }
            GUI.backgroundColor = prevBg;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
                _editingGraphIndex = -1;
            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                entry.GraphAsset.GraphName = _editGraphName;
                entry.GraphAsset.SheetId = _editSheetId;
                entry.GraphAsset.SheetName = _editSheetName;
                entry.GraphAsset.HeaderRowOffset = _editHeaderOffset;
                GraphDatabase.SaveGraphIfDirty(entry.GraphAsset);

                if (!string.IsNullOrEmpty(_editGraphName))
                {
                    var dir = Path.GetDirectoryName(entry.AssetPath);
                    var newPath = Path.Combine(dir, $"{_editGraphName}.datagraph");
                    if (newPath != entry.AssetPath)
                    {
                        var renameResult = AssetDatabase.MoveAsset(entry.AssetPath, newPath);
                        if (string.IsNullOrEmpty(renameResult))
                            entry.AssetPath = newPath;
                    }
                }

                entry.DisplayName = !string.IsNullOrEmpty(_editGraphName)
                    ? _editGraphName
                    : Path.GetFileNameWithoutExtension(entry.AssetPath);
                _editingGraphIndex = -1;
                RefreshGraphList();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (shouldDelete)
            {
                DeleteGraphAndGenerated(entry);
                _editingGraphIndex = -1;
            }
        }

        private void DrawResizeHandle()
        {
            var resizeRect = EditorGUILayout.GetControlRect(false, 4);
            EditorGUI.DrawRect(resizeRect, new Color(0.3f, 0.3f, 0.3f, 0.3f));
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDrag &&
                resizeRect.Contains(Event.current.mousePosition - new Vector2(0, 2)))
            {
                _splitRatio = Mathf.Clamp(Event.current.mousePosition.y / position.height, 0.25f, 0.8f);
                Repaint();
            }
        }

        // ==================== SETTINGS POPUP ====================

        private void DrawSettingsPopup()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", EditorStyles.miniButton, GUILayout.Width(50)))
                _showSettings = false;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Google Sheets Authentication", EditorStyles.miniBoldLabel);
            _authMethodIndex = GUILayout.Toolbar(_authMethodIndex, new[] { "API Key", "OAuth 2.0" });
            EditorGUILayout.Space(4);

            if (_authMethodIndex == 0)
            {
                _apiKey = EditorGUILayout.TextField("API Key", _apiKey);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Save API Key", GUILayout.Height(22)))
                    SaveApiKeyAndContinue();
            }
            else
            {
                _oauthClientId = EditorGUILayout.TextField("Client ID", _oauthClientId);
                _oauthClientSecret = EditorGUILayout.PasswordField("Client Secret", _oauthClientSecret);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Sign In", GUILayout.Height(22)))
                    SignInOAuthAndContinue();
            }

            if (!string.IsNullOrEmpty(_authStatus))
                EditorGUILayout.HelpBox(_authStatus, _authStatusType);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Paths", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Graphs", GUILayout.Width(55));
            _graphsPath = EditorGUILayout.TextField(_graphsPath);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                var selected = EditorUtility.OpenFolderPanel("Graphs Folder", _graphsPath, "");
                if (!string.IsNullOrEmpty(selected))
                    _graphsPath = selected.StartsWith(Application.dataPath)
                        ? "Assets" + selected.Substring(Application.dataPath.Length)
                        : selected;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output", GUILayout.Width(55));
            _outputPath = EditorGUILayout.TextField(_outputPath);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                var selected = EditorUtility.OpenFolderPanel("Output Folder", _outputPath, "");
                if (!string.IsNullOrEmpty(selected))
                    _outputPath = selected.StartsWith(Application.dataPath)
                        ? "Assets" + selected.Substring(Application.dataPath.Length)
                        : selected;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Sign Out / Clear Credentials", GUILayout.Height(22)))
                ClearAuth();

            EditorGUILayout.EndVertical();
        }

        // ==================== CONSOLE ====================

        private void DrawConsole()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Console", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.FlexibleSpace();

            DrawSeverityBadge(new Color(0.9f, 0.3f, 0.3f), _console.TotalErrors);
            DrawSeverityBadge(new Color(0.9f, 0.7f, 0.2f), _console.TotalWarnings);
            DrawSeverityBadge(new Color(0.1f, 0.7f, 0.4f), _console.TotalInfos);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(40)))
                _console.Clear();
            EditorGUILayout.EndHorizontal();

            if (_console.Groups.Count == 0)
            {
                EditorGUILayout.LabelField("Run a parse to see results here.",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Height(30));
                return;
            }

            _consoleScrollPosition = EditorGUILayout.BeginScrollView(_consoleScrollPosition);
            foreach (var group in _console.Groups)
                DrawLogGroup(group);
            EditorGUILayout.EndScrollView();
        }

        private void DrawLogGroup(GraphLogGroup group)
        {
            var statusColor = !group.IsComplete ? Color.gray
                : group.Success ? new Color(0.1f, 0.7f, 0.4f)
                : new Color(0.9f, 0.3f, 0.3f);
            var statusIcon = !group.IsComplete ? "\u25cb"
                : group.Success ? "\u2713" : "\u2717";

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var prev = GUI.contentColor;
            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField(statusIcon, GUILayout.Width(14));
            GUI.contentColor = prev;
            group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.GraphName, true);
            GUILayout.FlexibleSpace();
            if (group.ErrorCount > 0)
                DrawMiniCount(new Color(0.9f, 0.3f, 0.3f), group.ErrorCount);
            if (group.WarningCount > 0)
                DrawMiniCount(new Color(0.9f, 0.7f, 0.2f), group.WarningCount);
            if (group.IsComplete)
            {
                var elapsed = group.EndTime - group.StartTime;
                EditorGUILayout.LabelField($"{elapsed.TotalSeconds:F1}s",
                    EditorStyles.miniLabel, GUILayout.Width(32));
            }
            EditorGUILayout.EndHorizontal();

            if (!group.IsExpanded) return;

            EditorGUI.indentLevel++;
            foreach (var entry in group.Entries)
                DrawLogEntry(entry);
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
            EditorGUILayout.LabelField(entry.Timestamp.ToString("HH:mm:ss"),
                EditorStyles.miniLabel, GUILayout.Width(52));
            var prev = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(38));
            GUI.contentColor = prev;
            EditorGUILayout.LabelField(entry.Message, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSeverityBadge(Color color, int count)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(28));
            var rect = GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8));
            rect.y += 6;
            EditorGUI.DrawRect(rect, color);
            EditorGUILayout.LabelField(count.ToString(), EditorStyles.miniLabel, GUILayout.Width(14));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawMiniCount(Color color, int count)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(22));
            var rect = GUILayoutUtility.GetRect(6, 6, GUILayout.Width(6));
            rect.y += 7;
            EditorGUI.DrawRect(rect, color);
            EditorGUILayout.LabelField(count.ToString(), EditorStyles.miniLabel, GUILayout.Width(12));
            EditorGUILayout.EndHorizontal();
        }

        // ==================== ACTIONS ====================

        private void CreateNewGraph()
        {
            EnsureFolderExists(_graphsPath);

            var baseName = "NewGraph";
            var path = AssetDatabase.GenerateUniqueAssetPath($"{_graphsPath}/{baseName}.datagraph");

            try
            {
                var graph = GraphDatabase.CreateGraph<DataGraphAsset>(path);
                if (graph != null)
                {
                    AssetDatabase.Refresh();
                    RefreshGraphList();

                    for (int i = 0; i < _graphEntries.Count; i++)
                    {
                        if (_graphEntries[i].AssetPath == path)
                        {
                            _editingGraphIndex = i;
                            _editGraphName = "";
                            _editSheetId = "";
                            _editSheetName = "Sheet1";
                            _editHeaderOffset = 1;
                            Repaint();
                            break;
                        }
                    }
                }
            }
            catch
            {
                GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DataGraphAsset>();
            }
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void OpenGraphEditor(GraphEntry entry)
        {
            AssetDatabase.OpenAsset(
                AssetDatabase.LoadMainAssetAtPath(entry.AssetPath));
            JsonPreviewPanel.ShowForGraph(entry.GraphAsset);
        }

        private void DeleteGraphAndGenerated(GraphEntry entry)
        {
            var graphName = !string.IsNullOrEmpty(entry.GraphAsset.GraphName)
                ? entry.GraphAsset.GraphName
                : Path.GetFileNameWithoutExtension(entry.AssetPath);

            var generatedFolder = Path.Combine(_outputPath, graphName);
            if (AssetDatabase.IsValidFolder(generatedFolder))
                AssetDatabase.DeleteAsset(generatedFolder);

            AssetDatabase.DeleteAsset(entry.AssetPath);
            AssetDatabase.Refresh();
            RefreshGraphList();
        }

        private async void RunParseAsync()
        {
            _isRunning = true;
            _console.Clear();
            _cts = new CancellationTokenSource();
            Repaint();

            try
            {
                var command = new ParseGraphCommand();
                var selected = _graphEntries.Where(e => e.Selected).ToList();
                var tasks = new List<Task>();

                foreach (var entry in selected)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    var provider = ResolveProviderForGraph(entry.GraphAsset.SheetId);
                    if (provider == null)
                    {
                        var g = _console.BeginGroup(entry.DisplayName);
                        g.LogError("No provider available for this data source.");
                        g.Complete(false);
                        continue;
                    }

                    var log = _console.BeginGroup(entry.DisplayName);
                    var formats = new ParseGraphCommand.FormatSelection
                    {
                        GenerateSO = entry.GenerateSO,
                        GenerateJSON = entry.GenerateJSON,
                        GenerateBlob = entry.GenerateBlob,
                        GenerateQuantum = entry.GenerateQuantum
                    };
                    tasks.Add(command.ExecuteAsync(
                        entry.GraphAsset, provider, formats,
                        _outputPath, log, _cts.Token));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                var g = _console.BeginGroup("(error)");
                g.LogError(ex.Message);
                g.Complete(false);
            }
            finally
            {
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
                Repaint();
            }
        }

        private async void RunCreateAssetsAsync()
        {
            _isRunning = true;
            _console.Clear();
            _cts = new CancellationTokenSource();
            Repaint();

            try
            {
                var command = new ParseGraphCommand();
                var selected = _graphEntries.Where(e => e.Selected).ToList();
                var tasks = new List<Task>();

                foreach (var entry in selected)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    if (!entry.GenerateSO && !entry.GenerateBlob && !entry.GenerateQuantum)
                        continue;

                    var provider = ResolveProviderForGraph(entry.GraphAsset.SheetId);
                    if (provider == null)
                    {
                        var g = _console.BeginGroup(entry.DisplayName + " (Assets)");
                        g.LogError("No provider available for this data source.");
                        g.Complete(false);
                        continue;
                    }

                    if (entry.GenerateSO)
                    {
                        var log = _console.BeginGroup(entry.DisplayName + " (SO)");
                        tasks.Add(command.CreateSOAssetsAsync(
                            entry.GraphAsset, provider,
                            _outputPath, log, _cts.Token));
                    }

                    if (entry.GenerateBlob)
                    {
                        var log = _console.BeginGroup(entry.DisplayName + " (Blob)");
                        tasks.Add(command.CreateBlobAssetsAsync(
                            entry.GraphAsset, provider,
                            _outputPath, log, _cts.Token));
                    }

                    if (entry.GenerateQuantum)
                    {
                        var log = _console.BeginGroup(entry.DisplayName + " (Quantum)");
                        tasks.Add(command.CreateQuantumAssetsAsync(
                            entry.GraphAsset, provider,
                            _outputPath, log, _cts.Token));
                    }
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                var g = _console.BeginGroup("(error)");
                g.LogError(ex.Message);
                g.Complete(false);
            }
            finally
            {
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
                Repaint();
            }
        }

        // ==================== HELPERS ====================

        private void RefreshGraphList()
        {
            var previousSelection = new Dictionary<string, (bool selected, bool so, bool json, bool blob, bool quantum)>();
            foreach (var e in _graphEntries)
                previousSelection[e.AssetPath] = (e.Selected, e.GenerateSO, e.GenerateJSON, e.GenerateBlob, e.GenerateQuantum);

            _graphEntries.Clear();
            var guids = AssetDatabase.FindAssets("");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".datagraph")) continue;

                var graph = GraphDatabase.LoadGraph<DataGraphAsset>(path);
                if (graph == null) continue;

                var entry = new GraphEntry
                {
                    AssetPath = path,
                    GraphAsset = graph,
                    DisplayName = !string.IsNullOrEmpty(graph.GraphName)
                        ? graph.GraphName
                        : Path.GetFileNameWithoutExtension(path),
                    Selected = false,
                    GenerateSO = true,
                    GenerateJSON = true,
                    GenerateBlob = false,
                    GenerateQuantum = false
                };

                if (previousSelection.TryGetValue(path, out var prev))
                {
                    entry.Selected = prev.selected;
                    entry.GenerateSO = prev.so;
                    entry.GenerateJSON = prev.json;
                    entry.GenerateBlob = prev.blob;
                    entry.GenerateQuantum = prev.quantum;
                }

                _graphEntries.Add(entry);
            }
            Repaint();
        }

        private static ISheetProvider ResolveProvider()
        {
            if (ProviderRegistry.IsGoogleSheetsAvailable())
            {
                var gs = ProviderRegistry.CreateGoogleSheetsProvider();
                if (gs.IsAuthenticated) return gs;
            }

            if (ProviderRegistry.IsLocalFileAvailable())
                return ProviderRegistry.CreateLocalFileProvider();

            return null;
        }

        /// <summary>
        /// Resolves the appropriate provider for a specific graph based on its SheetId.
        /// File paths use LocalFile, URLs/IDs use Google Sheets.
        /// </summary>
        private static ISheetProvider ResolveProviderForGraph(string sheetId)
        {
            if (string.IsNullOrEmpty(sheetId))
                return ResolveProvider();

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

            return ResolveProvider();
        }

        private static bool IsLocalFilePath(string sheetId)
        {
            if (sheetId.StartsWith("Assets/") || sheetId.StartsWith("Assets\\"))
                return true;

            var ext = System.IO.Path.GetExtension(sheetId)?.ToLowerInvariant();
            return ext == ".csv" || ext == ".tsv" || ext == ".xlsx";
        }

        private static bool IsProviderConfigured()
        {
            if (ProviderRegistry.IsGoogleSheetsAvailable())
            {
                var gs = ProviderRegistry.CreateGoogleSheetsProvider();
                if (gs.IsAuthenticated) return true;
            }

            if (ProviderRegistry.IsLocalFileAvailable())
                return true;

            return false;
        }

        private void LoadAuthSettings()
        {
            _apiKey = EditorPrefs.GetString("DataGraph_Google_ApiKey", "");
            _oauthClientId = EditorPrefs.GetString("DataGraph_Google_OAuthClientId", "");
            _oauthClientSecret = EditorPrefs.GetString("DataGraph_Google_OAuthClientSecret", "");
            _authMethodIndex = !string.IsNullOrEmpty(_oauthClientId) ? 1 : 0;
        }

        private void SaveApiKeyAndContinue()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _authStatus = "API Key cannot be empty.";
                _authStatusType = MessageType.Error;
                return;
            }

            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                provider.GetType().GetMethod("ConfigureApiKey")
                    ?.Invoke(provider, new object[] { _apiKey.Trim() });
                _authStatus = "Connected.";
                _authStatusType = MessageType.Info;
                _currentScreen = Screen.Home;
                RefreshGraphList();
            }
            catch (Exception ex)
            {
                _authStatus = ex.Message;
                _authStatusType = MessageType.Error;
            }
        }

        private async void SignInOAuthAndContinue()
        {
            if (string.IsNullOrWhiteSpace(_oauthClientId) ||
                string.IsNullOrWhiteSpace(_oauthClientSecret))
            {
                _authStatus = "Client ID and Secret required.";
                _authStatusType = MessageType.Error;
                return;
            }

            _authStatus = "Opening browser...";
            _authStatusType = MessageType.Info;
            Repaint();

            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                var method = provider.GetType().GetMethod("ConfigureOAuthAsync");
                var task = (Task<Runtime.Result<bool>>)method?.Invoke(provider, new object[]
                {
                    _oauthClientId.Trim(),
                    _oauthClientSecret.Trim(),
                    CancellationToken.None
                });

                var result = await task;
                if (result.IsSuccess)
                {
                    _authStatus = "Signed in.";
                    _authStatusType = MessageType.Info;
                    _currentScreen = Screen.Home;
                    RefreshGraphList();
                }
                else
                {
                    _authStatus = result.Error;
                    _authStatusType = MessageType.Error;
                }
            }
            catch (Exception ex)
            {
                _authStatus = ex.Message;
                _authStatusType = MessageType.Error;
            }

            Repaint();
        }

        private void ClearAuth()
        {
            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                provider.GetType().GetMethod("SignOut")?.Invoke(provider, null);
                _apiKey = "";
                _oauthClientId = "";
                _oauthClientSecret = "";
                _authStatus = "Credentials cleared.";
                _authStatusType = MessageType.Info;
            }
            catch (Exception ex)
            {
                _authStatus = ex.Message;
                _authStatusType = MessageType.Error;
            }
        }

        private class GraphEntry
        {
            public string AssetPath;
            public DataGraphAsset GraphAsset;
            public string DisplayName;
            public bool Selected;
            public bool GenerateSO;
            public bool GenerateJSON;
            public bool GenerateBlob;
            public bool GenerateQuantum;
        }
    }
}
