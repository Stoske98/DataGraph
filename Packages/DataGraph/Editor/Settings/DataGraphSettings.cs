using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DataGraph.Editor
{
    /// <summary>
    /// Project-wide settings for DataGraph, stored as JSON in
    /// ProjectSettings/DataGraphSettings.asset. Shared through source control.
    /// </summary>
    internal sealed class DataGraphSettings : ScriptableObject
    {
        private const string AssetPath = "ProjectSettings/DataGraphSettings.asset";

        [SerializeField] private PathSettings _paths = new();
        [SerializeField] private CodeGenSettings _codeGeneration = new();
        [SerializeField] private EditorBehavior _editor = new();

        public PathSettings Paths => _paths;
        public CodeGenSettings CodeGeneration => _codeGeneration;
        public EditorBehavior Editor => _editor;

        private static DataGraphSettings _instance;

        public static DataGraphSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = LoadOrCreate();
                return _instance;
            }
        }

        public void Save()
        {
            var json = EditorJsonUtility.ToJson(this, prettyPrint: true);
            var dir = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(AssetPath, json);
        }

        private static DataGraphSettings LoadOrCreate()
        {
            var settings = CreateInstance<DataGraphSettings>();
            if (File.Exists(AssetPath))
            {
                var json = File.ReadAllText(AssetPath);
                EditorJsonUtility.FromJsonOverwrite(json, settings);
            }
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }

        [Serializable]
        internal sealed class PathSettings
        {
            [SerializeField] private string _graphsFolder = "Assets/DataGraph/Graphs";
            [SerializeField] private string _generatedFolder = "Assets/DataGraph/Generated";

            public string GraphsFolder
            {
                get => _graphsFolder;
                set => _graphsFolder = value?.Replace('\\', '/').TrimEnd('/') ?? _graphsFolder;
            }

            public string GeneratedFolder
            {
                get => _generatedFolder;
                set => _generatedFolder = value?.Replace('\\', '/').TrimEnd('/') ?? _generatedFolder;
            }
        }

        [Serializable]
        internal sealed class CodeGenSettings
        {
            [SerializeField] private string _namespace = "DataGraph.Data";
            [SerializeField] private bool _autoParseOnSave = true;

            public string Namespace
            {
                get => _namespace;
                set => _namespace = string.IsNullOrWhiteSpace(value) ? "DataGraph.Data" : value.Trim();
            }

            public bool AutoParseOnSave
            {
                get => _autoParseOnSave;
                set => _autoParseOnSave = value;
            }
        }

        [Serializable]
        internal sealed class EditorBehavior
        {
            [SerializeField] private bool _autoRefreshJsonPreview = true;

            public bool AutoRefreshJsonPreview
            {
                get => _autoRefreshJsonPreview;
                set => _autoRefreshJsonPreview = value;
            }
        }
    }
}
