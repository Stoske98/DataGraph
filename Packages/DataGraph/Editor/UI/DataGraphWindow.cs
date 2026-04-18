using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.Adapter;
using DataGraph.Editor.Commands;
using DataGraph.Editor.GraphView;
using DataGraph.Editor.Public;
using DataGraph.Editor.UI;
using DataGraph.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DataGraph.Editor
{
    /// <summary>
    /// Unified DataGraph editor window.
    /// Left: graph list (top) + JSON preview (bottom).
    /// Center: GraphView node editor.
    /// Right bottom: Console.
    /// Uses OnGUI for panels, VisualElement host for GraphView.
    /// </summary>
    internal sealed class DataGraphWindow : EditorWindow
    {
        [SerializeField] private DataGraphConsole _console = new();
        [SerializeField] private List<GraphEntry> _graphEntries = new();
        [SerializeField] private float _leftWidth = 240f;
        [SerializeField] private float _bottomHeight = 180f;
        [SerializeField] private Vector2 _graphListScroll;
        [SerializeField] private Vector2 _consoleScroll;
        [SerializeField] private Vector2 _jsonScroll;
        [SerializeField] private int _editingIndex = -1;
        [SerializeField] private string _editGraphName;
        [SerializeField] private string _editSheetId;
        [SerializeField] private string _editSheetName;
        [SerializeField] private int _editHeaderOffset;
        [SerializeField] private string _activeGraphPath;

        private DataGraphView _graphView;
        private DataGraphSearchWindow _searchWindow;
        private VisualElement _graphViewHost;
        private DataGraphAsset _activeGraph;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private bool _needsRefresh = true;

        private string _jsonPreviewText = "";

        private string OutputPath => DataGraphSettings.Instance.Paths.GeneratedFolder;

        [MenuItem("DataGraph/Editor")]
        public static void Open()
        {
            var wnd = GetWindow<DataGraphWindow>();
            wnd.titleContent = new GUIContent("DataGraph");
            wnd.minSize = new Vector2(900, 600);
        }

        [MenuItem("DataGraph/Settings")]
        public static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/DataGraph");
        }

        private void OnEnable()
        {
            _needsRefresh = true;
            wantsMouseMove = true;
            EditorApplication.projectChanged += OnProjectChanged;

            if (!string.IsNullOrEmpty(_activeGraphPath))
            {
                _activeGraph = AssetDatabase.LoadAssetAtPath<DataGraphAsset>(_activeGraphPath);
            }
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnProjectChanged()
        {
            Repaint();
        }

        private void OnFocus()
        {
            Repaint();
        }

        private void CreateGUI()
        {
            _graphViewHost = new VisualElement();
            _graphViewHost.name = "graphview-host";
            _graphViewHost.pickingMode = PickingMode.Position;
            rootVisualElement.Add(_graphViewHost);

            _graphView = new DataGraphView();
            _graphView.style.flexGrow = 1;
            _graphViewHost.Add(_graphView);

            _searchWindow = CreateInstance<DataGraphSearchWindow>();
            _searchWindow.Initialize(_graphView, this);
            _graphView.SetSearchWindow(_searchWindow);
            _graphView.OnGraphStructureChanged += OnGraphStructureChanged;

            UpdateGraphViewRect();

            if (_activeGraph != null)
                _graphView.LoadGraph(_activeGraph);
        }

        private void OnGraphStructureChanged()
        {
            RefreshJsonPreview(manual: false);
        }

        private void UpdateGraphViewRect()
        {
            if (_graphViewHost == null) return;

            var graphLeft = _leftWidth + 4;
            var graphTop = 0f;
            var graphWidth = position.width - graphLeft;
            var graphHeight = position.height - _bottomHeight - 4;

            if (graphWidth < 100) graphWidth = 100;
            if (graphHeight < 100) graphHeight = 100;

            _graphViewHost.style.position = Position.Absolute;
            _graphViewHost.style.left = graphLeft;
            _graphViewHost.style.top = graphTop;
            _graphViewHost.style.width = graphWidth;
            _graphViewHost.style.height = graphHeight;
        }

        private void OnGUI()
        {
            if (_needsRefresh)
            {
                RefreshGraphList();
                _needsRefresh = false;
            }

            UpdateGraphViewRect();
            DrawResizeHandles();
            DrawLeftPanel();
            DrawBottomPanel();
        }

        // ==================== LEFT PANEL ====================

        private void DrawLeftPanel()
        {
            var leftRect = new Rect(0, 0, _leftWidth, position.height - _bottomHeight - 4);
            GUILayout.BeginArea(leftRect);
            EditorGUILayout.BeginVertical();

            DrawGraphListHeader();
            DrawGraphList();
            DrawActionButtons();

            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawGraphListHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Graphs", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "Create new graph"),
                EditorStyles.toolbarButton, GUILayout.Width(28), GUILayout.Height(18)))
            {
                CreateNewGraph();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphList()
        {
            _graphListScroll = EditorGUILayout.BeginScrollView(_graphListScroll,
                GUILayout.ExpandHeight(true));

            if (_graphEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No graphs found.\nClick '+' to create one.",
                    MessageType.Info);
            }

            var blobAvailable = ProviderRegistry.IsBlobAvailable();
            var quantumAvailable = ProviderRegistry.IsQuantumAvailable();

            for (int i = 0; i < _graphEntries.Count; i++)
            {
                if (_editingIndex == i)
                    DrawGraphEditRow(_graphEntries[i], i);
                else
                    DrawGraphRow(_graphEntries[i], i, blobAvailable, quantumAvailable);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGraphRow(GraphEntry entry, int index, bool blobAvailable, bool quantumAvailable)
        {
            var isActive = _activeGraph == entry.GraphAsset;
            if (isActive)
            {
                var bg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.6f, 1f, 0.3f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = bg;
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            }

            // Row 1: checkbox + name + edit
            EditorGUILayout.BeginHorizontal();
            entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(16));

            if (GUILayout.Button(entry.DisplayName, EditorStyles.label))
                SelectGraph(entry.GraphAsset);

            if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                _editingIndex = index;
                _editGraphName = entry.GraphAsset.GraphName ?? "";
                _editSheetId = entry.GraphAsset.SheetId ?? "";
                _editSheetName = entry.GraphAsset.SheetName ?? "Sheet1";
                _editHeaderOffset = entry.GraphAsset.HeaderRowOffset;
            }
            EditorGUILayout.EndHorizontal();

            // Row 2: format toggles
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(22);
            entry.GenerateSO = GUILayout.Toggle(entry.GenerateSO, "SO", GUILayout.Width(34));
            entry.GenerateJSON = GUILayout.Toggle(entry.GenerateJSON, "JSON", GUILayout.Width(52));
            if (blobAvailable)
                entry.GenerateBlob = GUILayout.Toggle(entry.GenerateBlob, "Blob", GUILayout.Width(42));
            if (quantumAvailable)
                entry.GenerateQuantum = GUILayout.Toggle(entry.GenerateQuantum, "Quantum", GUILayout.Width(68));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawGraphEditRow(GraphEntry entry, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Edit: {entry.DisplayName}", EditorStyles.boldLabel);

            var rawName = EditorGUILayout.TextField("Name", _editGraphName);
            _editGraphName = rawName.Replace(" ", "");

            EditorGUILayout.BeginHorizontal();
            _editSheetId = EditorGUILayout.TextField("Source", _editSheetId);
            if (GUILayout.Button("...", GUILayout.Width(24)))
            {
                var path = EditorUtility.OpenFilePanel(
                    "Select Data File", "Assets", "csv,tsv,xlsx");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    _editSheetId = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            _editSheetName = EditorGUILayout.TextField("Sheet Tab", _editSheetName);
            _editHeaderOffset = EditorGUILayout.IntField("Header Offset", _editHeaderOffset);

            EditorGUILayout.BeginHorizontal();

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Delete", GUILayout.Width(55)))
            {
                if (EditorUtility.DisplayDialog("Delete Graph",
                    $"Delete '{entry.DisplayName}' and all generated files?",
                    "Delete", "Cancel"))
                {
                    GUI.backgroundColor = prevBg;
                    DeleteGraph(entry);
                    _editingIndex = -1;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }
            GUI.backgroundColor = prevBg;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(55)))
                _editingIndex = -1;
            if (GUILayout.Button("Save", GUILayout.Width(55)))
            {
                Undo.RecordObject(entry.GraphAsset, "Edit Graph");
                entry.GraphAsset.GraphName = _editGraphName;
                entry.GraphAsset.SheetId = _editSheetId;
                entry.GraphAsset.SheetName = _editSheetName;
                entry.GraphAsset.HeaderRowOffset = _editHeaderOffset;
                EditorUtility.SetDirty(entry.GraphAsset);
                AssetDatabase.SaveAssets();

                // Rename asset file to match graph name
                if (!string.IsNullOrEmpty(_editGraphName))
                {
                    var dir = Path.GetDirectoryName(entry.AssetPath);
                    var newPath = Path.Combine(dir, $"{_editGraphName}.asset");
                    if (newPath != entry.AssetPath && !File.Exists(newPath))
                    {
                        var result = AssetDatabase.MoveAsset(entry.AssetPath, newPath);
                        if (string.IsNullOrEmpty(result))
                            entry.AssetPath = newPath;
                    }
                }

                entry.DisplayName = !string.IsNullOrEmpty(_editGraphName)
                    ? _editGraphName
                    : Path.GetFileNameWithoutExtension(entry.AssetPath);
                _editingIndex = -1;
                _needsRefresh = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            var selectedCount = _graphEntries.Count(e => e.Selected);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = selectedCount > 0 && !_isRunning;
            if (GUILayout.Button($"Parse ({selectedCount})", GUILayout.Height(24)))
                RunParseAsync();
            if (GUILayout.Button($"Create Assets ({selectedCount})", GUILayout.Height(24)))
                RunCreateAssetsAsync();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // ==================== BOTTOM PANEL ====================

        private void DrawBottomPanel()
        {
            var bottomY = position.height - _bottomHeight;
            var jsonWidth = _leftWidth;
            var consoleX = _leftWidth + 4;
            var consoleWidth = position.width - consoleX;

            var jsonRect = new Rect(0, bottomY, jsonWidth, _bottomHeight);
            GUILayout.BeginArea(jsonRect);
            DrawJsonPreview();
            GUILayout.EndArea();

            var consoleRect = new Rect(consoleX, bottomY, consoleWidth, _bottomHeight);
            GUILayout.BeginArea(consoleRect);
            DrawConsole();
            GUILayout.EndArea();
        }

        private void DrawJsonPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("JSON Preview", EditorStyles.miniLabel, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55)))
                RefreshJsonPreview(manual: true);
            EditorGUILayout.EndHorizontal();

            _jsonScroll = EditorGUILayout.BeginScrollView(_jsonScroll);
            EditorGUILayout.TextArea(_jsonPreviewText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawConsole()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Console", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.FlexibleSpace();

            DrawSeverityBadge(new Color(0.1f, 0.7f, 0.4f), _console.TotalInfos);
            DrawSeverityBadge(new Color(0.85f, 0.65f, 0.1f), _console.TotalWarnings);
            DrawSeverityBadge(new Color(0.9f, 0.3f, 0.3f), _console.TotalErrors);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(40)))
                _console.Clear();
            EditorGUILayout.EndHorizontal();

            if (_console.Groups.Count == 0)
            {
                EditorGUILayout.LabelField("Run a parse to see results here.",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Height(24));
            }
            else
            {
                _consoleScroll = EditorGUILayout.BeginScrollView(_consoleScroll);
                foreach (var group in _console.Groups)
                    DrawLogGroup(group);
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawSeverityBadge(Color color, int count)
        {
            if (count <= 0) return;
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(count.ToString(), EditorStyles.miniButton,
                GUILayout.Width(24), GUILayout.Height(16));
            GUI.backgroundColor = prev;
        }

        private void DrawLogGroup(GraphLogGroup group)
        {
            EditorGUILayout.BeginHorizontal();
            var prev = GUI.contentColor;
            GUI.contentColor = group.Success
                ? new Color(0.1f, 0.7f, 0.4f)
                : new Color(0.9f, 0.3f, 0.3f);
            group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.GraphName, true);
            EditorGUILayout.EndHorizontal();

            if (!group.IsExpanded) return;

            EditorGUI.indentLevel++;
            foreach (var entry in group.Entries)
            {
                var color = entry.Severity switch
                {
                    LogSeverity.Error => new Color(0.9f, 0.3f, 0.3f),
                    LogSeverity.Warning => new Color(0.85f, 0.65f, 0.1f),
                    LogSeverity.Success => new Color(0.1f, 0.7f, 0.4f),
                    _ => Color.gray
                };

                var rect = EditorGUILayout.BeginHorizontal();
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    EditorGUIUtility.systemCopyBuffer = entry.Message;
                    Debug.Log($"[DataGraph] Copied: {entry.Message}");
                    Event.current.Use();
                }
                prev = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.LabelField(entry.Timestamp.ToString("HH:mm:ss"),
                    EditorStyles.miniLabel, GUILayout.Width(58));
                EditorGUILayout.LabelField(entry.Message, EditorStyles.miniLabel);
                GUI.contentColor = prev;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        // ==================== RESIZE HANDLES ====================

        private bool _resizingLeft;
        private bool _resizingBottom;

        private void DrawResizeHandles()
        {
            var vertHandle = new Rect(_leftWidth - 2, 0, 6, position.height - _bottomHeight);
            EditorGUI.DrawRect(new Rect(_leftWidth - 1, 0, 2, position.height - _bottomHeight),
                new Color(0.15f, 0.15f, 0.15f, 1f));
            EditorGUIUtility.AddCursorRect(vertHandle, MouseCursor.ResizeHorizontal);

            var horizY = position.height - _bottomHeight - 2;
            var horizHandle = new Rect(0, horizY, position.width, 6);
            EditorGUI.DrawRect(new Rect(0, horizY, position.width, 2),
                new Color(0.15f, 0.15f, 0.15f, 1f));
            EditorGUIUtility.AddCursorRect(horizHandle, MouseCursor.ResizeVertical);

            var e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (vertHandle.Contains(e.mousePosition))
                {
                    _resizingLeft = true;
                    e.Use();
                }
                else if (horizHandle.Contains(e.mousePosition))
                {
                    _resizingBottom = true;
                    e.Use();
                }
            }

            if (e.type == EventType.MouseDrag)
            {
                if (_resizingLeft)
                {
                    _leftWidth = Mathf.Clamp(e.mousePosition.x, 180, position.width * 0.4f);
                    e.Use();
                    Repaint();
                }
                if (_resizingBottom)
                {
                    _bottomHeight = Mathf.Clamp(position.height - e.mousePosition.y, 80, position.height * 0.5f);
                    e.Use();
                    Repaint();
                }
            }

            if (e.type == EventType.MouseUp)
            {
                _resizingLeft = false;
                _resizingBottom = false;
            }
        }

        // ==================== GRAPH MANAGEMENT ====================

        private void SelectGraph(DataGraphAsset graphAsset)
        {
            _activeGraph = graphAsset;
            _activeGraphPath = graphAsset != null ? AssetDatabase.GetAssetPath(graphAsset) : "";
            _graphView?.LoadGraph(graphAsset);
            RefreshJsonPreview(manual: true);
            Repaint();
        }

        private void CreateNewGraph()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Dictionary"), false, () => DoCreateGraph(GraphType.Dictionary));
            menu.AddItem(new GUIContent("Array"), false, () => DoCreateGraph(GraphType.Array));
            menu.AddItem(new GUIContent("Object"), false, () => DoCreateGraph(GraphType.Object));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Enum"), false, () => DoCreateGraph(GraphType.Enum));
            menu.AddItem(new GUIContent("Flag"), false, () => DoCreateGraph(GraphType.Flag));
            menu.ShowAsContext();
        }

        private void DoCreateGraph(GraphType graphType)
        {
            var folder = DataGraphSettings.Instance.Paths.GraphsFolder;
            EnsureFolderExists(folder);

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/NewGraph.asset");
            var asset = CreateInstance<DataGraphAsset>();
            asset.GraphType = graphType;
            asset.InitializeStructure(Path.GetFileNameWithoutExtension(path));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            _needsRefresh = true;
            SelectGraph(asset);
        }

        private void DeleteGraph(GraphEntry entry)
        {
            var graphName = !string.IsNullOrEmpty(entry.GraphAsset.GraphName)
                ? entry.GraphAsset.GraphName
                : Path.GetFileNameWithoutExtension(entry.AssetPath);

            var graphType = entry.GraphAsset.GraphType;

            // 1. Delete main generated folder (SO, JSON, Blob code, Blob data)
            var generatedFolder = Path.Combine(OutputPath, graphName);
            if (AssetDatabase.IsValidFolder(generatedFolder))
                AssetDatabase.DeleteAsset(generatedFolder);

            // 2. Delete Quantum generated folder
            var quantumFolder = $"Assets/QuantumUser/Simulation/DataGraph/{graphName}";
            if (AssetDatabase.IsValidFolder(quantumFolder))
                AssetDatabase.DeleteAsset(quantumFolder);

            // 3. Delete Enum/Flag generated files
            if (graphType == GraphType.Enum || graphType == GraphType.Flag)
            {
                var typeName = GetDefinitionTypeName(entry.GraphAsset);
                if (!string.IsNullOrEmpty(typeName))
                {
                    var subfolder = graphType == GraphType.Enum ? "Enums" : "Flags";

                    // Could be in Generated folder (no Quantum) or Quantum folder
                    var soEnumPath = Path.Combine(OutputPath, subfolder, $"{typeName}.cs");
                    if (File.Exists(soEnumPath))
                        AssetDatabase.DeleteAsset(soEnumPath);

                    var quantumEnumPath = $"Assets/QuantumUser/Simulation/DataGraph/{subfolder}/{typeName}.cs";
                    if (File.Exists(quantumEnumPath))
                        AssetDatabase.DeleteAsset(quantumEnumPath);

                    CleanEmptyFolder(Path.Combine(OutputPath, subfolder));
                    CleanEmptyFolder($"Assets/QuantumUser/Simulation/DataGraph/{subfolder}");
                }
            }

            // 4. Clean registry entries
            CleanRegistryForGraph(graphName);

            // 5. Delete the graph asset itself
            AssetDatabase.DeleteAsset(entry.AssetPath);
            AssetDatabase.Refresh();

            if (_activeGraph == entry.GraphAsset)
            {
                _activeGraph = null;
                _graphView?.ClearGraph();
            }
            _needsRefresh = true;
        }

        /// <summary>
        /// Extracts the TypeName from an Enum/Flag graph's definition node.
        /// </summary>
        private static string GetDefinitionTypeName(DataGraphAsset graphAsset)
        {
            foreach (var node in graphAsset.Nodes)
            {
                if (node.TypeName is NodeTypeRegistry.Types.Enum
                    or NodeTypeRegistry.Types.Flag)
                    return node.GetProperty("TypeName", "");
            }
            return null;
        }

        /// <summary>
        /// Removes SO and Blob entries from the DataGraphRegistry for a deleted graph.
        /// </summary>
        private static void CleanRegistryForGraph(string graphName)
        {
            var guids = AssetDatabase.FindAssets("t:DataGraphRegistry");
            if (guids.Length == 0) return;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var registry = AssetDatabase.LoadAssetAtPath<DataGraph.Runtime.DataGraphRegistry>(path);
            if (registry == null) return;

            Undo.RecordObject(registry, "Clean Registry");
            registry.UnregisterSO(graphName);
            registry.UnregisterBlob(graphName);
            registry.CleanUp();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
        }

        private static void CleanEmptyFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) return;
            var remaining = AssetDatabase.FindAssets("", new[] { folderPath });
            if (remaining.Length == 0)
                AssetDatabase.DeleteAsset(folderPath);
        }

        private void RefreshGraphList()
        {
            var prevSelection = new Dictionary<string, (bool sel, bool so, bool json, bool blob, bool quantum)>();
            foreach (var e in _graphEntries)
                prevSelection[e.AssetPath] = (e.Selected, e.GenerateSO, e.GenerateJSON, e.GenerateBlob, e.GenerateQuantum);

            _graphEntries.Clear();
            var guids = AssetDatabase.FindAssets("t:DataGraphAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<DataGraphAsset>(path);
                if (asset == null) continue;

                var entry = new GraphEntry
                {
                    AssetPath = path,
                    GraphAsset = asset,
                    DisplayName = !string.IsNullOrEmpty(asset.GraphName)
                        ? asset.GraphName
                        : Path.GetFileNameWithoutExtension(path)
                };

                if (prevSelection.TryGetValue(path, out var prev))
                {
                    entry.Selected = prev.sel;
                    entry.GenerateSO = prev.so;
                    entry.GenerateJSON = prev.json;
                    entry.GenerateBlob = prev.blob;
                    entry.GenerateQuantum = prev.quantum;
                }

                _graphEntries.Add(entry);
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

        // ==================== PARSE & CREATE ASSETS ====================

        private async void RunParseAsync()
        {
            var selected = _graphEntries.Where(e => e.Selected).ToList();
            if (selected.Count == 0) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _console.Clear();
            Repaint();

            try
            {
                var validator = new GraphValidator();
                var command = new ParseGraphCommand();

                foreach (var entry in selected)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    // Validate graph before parsing
                    var validation = validator.Validate(entry.GraphAsset);
                    if (!validation.IsValid)
                    {
                        var g = _console.BeginGroup(entry.DisplayName);
                        foreach (var err in validation.Errors)
                            g.LogError(err);
                        foreach (var warn in validation.Warnings)
                            g.LogWarning(warn);
                        g.Complete(false);
                        continue;
                    }

                    // Log warnings even if valid
                    if (validation.Warnings.Count > 0)
                    {
                        var warnGroup = _console.BeginGroup(entry.DisplayName + " (warnings)");
                        foreach (var warn in validation.Warnings)
                            warnGroup.LogWarning(warn);
                        warnGroup.Complete(true);
                    }

                    var provider = ResolveProviderForGraph(entry.GraphAsset.SheetId);
                    if (provider == null)
                    {
                        var g = _console.BeginGroup(entry.DisplayName);
                        g.LogError("No provider available.");
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
                    await command.ExecuteAsync(
                        entry.GraphAsset, provider, formats,
                        OutputPath, log, _cts.Token);
                    Repaint();
                }
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
            var selected = _graphEntries.Where(e => e.Selected).ToList();
            if (selected.Count == 0) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            Repaint();

            try
            {
                var validator = new GraphValidator();
                var command = new ParseGraphCommand();

                foreach (var entry in selected)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    var validation = validator.Validate(entry.GraphAsset);
                    if (!validation.IsValid)
                    {
                        var g = _console.BeginGroup(entry.DisplayName);
                        foreach (var err in validation.Errors)
                            g.LogError(err);
                        g.Complete(false);
                        continue;
                    }

                    var provider = ResolveProviderForGraph(entry.GraphAsset.SheetId);
                    if (provider == null) continue;

                    if (entry.GenerateSO)
                    {
                        var log = _console.BeginGroup(entry.DisplayName + " (SO)");
                        await command.CreateSOAssetsAsync(
                            entry.GraphAsset, provider,
                            OutputPath, log, _cts.Token);
                    }
                    if (entry.GenerateBlob)
                    {
                        var log = _console.BeginGroup(entry.DisplayName + " (Blob)");
                        await command.CreateBlobAssetsAsync(
                            entry.GraphAsset, provider,
                            OutputPath, log, _cts.Token);
                    }
                    if (entry.GenerateQuantum)
                    {
                        var log = _console.BeginGroup(entry.DisplayName + " (Quantum)");
                        await command.CreateQuantumAssetsAsync(
                            entry.GraphAsset, provider,
                            OutputPath, log, _cts.Token);
                    }
                    Repaint();
                }
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

        // ==================== JSON PREVIEW ====================

        private async void RefreshJsonPreview(bool manual = false)
        {
            if (!manual && !DataGraphSettings.Instance.Editor.AutoRefreshJsonPreview)
                return;

            if (_activeGraph == null || string.IsNullOrEmpty(_activeGraph.SheetId))
            {
                _jsonPreviewText = "Select a graph with a data source.";
                return;
            }

            var provider = ResolveProviderForGraph(_activeGraph.SheetId);
            if (provider == null)
            {
                _jsonPreviewText = "No provider available.";
                return;
            }

            try
            {
                var adapter = new GraphModelAdapter();
                var adaptResult = adapter.ReadGraph(_activeGraph);
                if (adaptResult.IsFailure)
                {
                    _jsonPreviewText = adaptResult.Error;
                    return;
                }

                var sheetRef = new SheetReference(
                    _activeGraph.SheetId, _activeGraph.HeaderRowOffset, _activeGraph.SheetName);
                var fetchResult = await provider.FetchAsync(sheetRef, CancellationToken.None);
                if (fetchResult.IsFailure)
                {
                    _jsonPreviewText = fetchResult.Error;
                    return;
                }

                var parser = new Parsing.ParserEngine();
                var parseResult = parser.Parse(fetchResult.Value, adaptResult.Value);
                if (parseResult.IsFailure)
                {
                    _jsonPreviewText = parseResult.Error;
                    return;
                }

                var jsonSerializer = new Serialization.JsonDataSerializer();
                var jsonResult = jsonSerializer.Serialize(parseResult.Value);
                _jsonPreviewText = jsonResult.IsSuccess ? jsonResult.Value : jsonResult.Error;
            }
            catch (Exception ex)
            {
                _jsonPreviewText = $"Error: {ex.Message}";
            }
            Repaint();
        }

        // ==================== PROVIDER RESOLUTION ====================

        private static ISheetProvider ResolveProviderForGraph(string sheetId)
        {
            // 1. Local file: Assets/ prefix or .csv/.tsv/.xlsx extension
            if (IsLocalFilePath(sheetId))
            {
                if (ProviderRegistry.IsLocalFileAvailable())
                    return ProviderRegistry.CreateLocalFileProvider();
            }

            // 2. OneDrive: onedrive:// prefix, 1drv.ms, sharepoint.com
            if (ProviderRegistry.IsOneDrivePath(sheetId))
            {
                if (ProviderRegistry.IsOneDriveAvailable())
                    return ProviderRegistry.CreateOneDriveProvider();
            }

            // 3. Everything else: assume Google Sheets ID
            if (!IsLocalFilePath(sheetId) && !ProviderRegistry.IsOneDrivePath(sheetId))
            {
                if (ProviderRegistry.IsGoogleSheetsAvailable())
                {
                    var gs = ProviderRegistry.CreateGoogleSheetsProvider();
                    if (gs.IsAuthenticated) return gs;
                }
            }

            // Fallback chain: try any available provider
            if (ProviderRegistry.IsLocalFileAvailable())
                return ProviderRegistry.CreateLocalFileProvider();
            if (ProviderRegistry.IsOneDriveAvailable())
                return ProviderRegistry.CreateOneDriveProvider();
            if (ProviderRegistry.IsGoogleSheetsAvailable())
                return ProviderRegistry.CreateGoogleSheetsProvider();

            return null;
        }

        private static bool IsLocalFilePath(string sheetId)
        {
            if (string.IsNullOrEmpty(sheetId)) return false;
            if (sheetId.StartsWith("Assets/") || sheetId.StartsWith("Assets\\")) return true;
            var ext = Path.GetExtension(sheetId)?.ToLowerInvariant();
            return ext is ".csv" or ".tsv" or ".xlsx";
        }

        [Serializable]
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
