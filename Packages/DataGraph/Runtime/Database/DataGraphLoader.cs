using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Unified runtime loader for all DataGraph databases.
    /// Reads a DataGraphRegistry asset and loads all SO databases
    /// into Database and all Blob databases into BlobDatabase.
    /// Place on a GameObject in your scene and assign the registry.
    /// On destroy, unregisters only the entries loaded by this instance.
    /// </summary>
    public sealed class DataGraphLoader : MonoBehaviour
    {
        [SerializeField]
        private DataGraphRegistry _registry;

        private readonly List<Type> _registeredSOEntries = new();
        private readonly List<Type> _registeredBlobEntries = new();

        private void Awake()
        {
            if (_registry == null)
            {
                Debug.LogWarning("[DataGraph] No registry assigned to DataGraphLoader.");
                return;
            }

            _registry.CleanUp();
            LoadSODatabases();
            LoadBlobDatabases();
        }

        private void OnDestroy()
        {
            foreach (var entryType in _registeredSOEntries)
                Database.Unregister(entryType);
            _registeredSOEntries.Clear();

            TryUnregisterBlobEntries();
            _registeredBlobEntries.Clear();
        }

        private void LoadSODatabases()
        {
            foreach (var asset in _registry.SOAssets)
            {
                if (asset == null) continue;
                asset.Register();
                _registeredSOEntries.Add(asset.EntryType);
            }
        }

        private void LoadBlobDatabases()
        {
            foreach (var entry in _registry.BlobEntries)
            {
                if (string.IsNullOrEmpty(entry.builderTypeName) ||
                    string.IsNullOrEmpty(entry.blobFileName))
                    continue;

                LoadBlob(entry, _registeredBlobEntries);
            }
        }

        private static void LoadBlob(DataGraphRegistry.BlobEntry entry, List<Type> registeredBlobEntries)
        {
            var builderType = Type.GetType(entry.builderTypeName);
            if (builderType == null)
            {
                Debug.LogWarning($"[DataGraph] Blob builder type '{entry.builderTypeName}' not found.");
                return;
            }

            var loadMethod = builderType.GetMethod("Load",
                BindingFlags.Public | BindingFlags.Static);
            if (loadMethod == null)
            {
                Debug.LogWarning($"[DataGraph] Load method not found on '{entry.builderTypeName}'.");
                return;
            }

            var blobPath = Path.Combine(Application.streamingAssetsPath, "DataGraph", entry.blobFileName);

#if UNITY_EDITOR
            var editorPath = FindBlobInEditor(entry.blobFileName);
            if (!string.IsNullOrEmpty(editorPath))
                blobPath = editorPath;
#endif

            if (!File.Exists(blobPath))
            {
                Debug.LogWarning($"[DataGraph] Blob file not found: {blobPath}");
                return;
            }

            try
            {
                var blobRef = loadMethod.Invoke(null, new object[] { blobPath });

                var registerMethod = builderType.GetMethod("Register",
                    BindingFlags.Public | BindingFlags.Static);
                if (registerMethod == null)
                {
                    Debug.LogWarning($"[DataGraph] Register method not found on '{entry.builderTypeName}'.");
                    return;
                }

                var blobDbType = Type.GetType("DataGraph.Data.BlobDatabase, DataGraph.Blob");
                var before = blobDbType != null
                    ? new HashSet<Type>(GetRegisteredBlobEntryTypes(blobDbType))
                    : new HashSet<Type>();

                registerMethod.Invoke(null, new[] { blobRef });

                if (blobDbType != null)
                {
                    foreach (var t in GetRegisteredBlobEntryTypes(blobDbType))
                    {
                        if (!before.Contains(t))
                            registeredBlobEntries.Add(t);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DataGraph] Blob load failed for '{entry.blobFileName}': {ex.Message}");
            }
        }

        private void TryUnregisterBlobEntries()
        {
            if (_registeredBlobEntries.Count == 0) return;

            var blobDbType = Type.GetType("DataGraph.Data.BlobDatabase, DataGraph.Blob");
            if (blobDbType == null) return;

            var unregisterMethod = blobDbType.GetMethod("Unregister",
                BindingFlags.Public | BindingFlags.Static);
            if (unregisterMethod == null) return;

            foreach (var entryType in _registeredBlobEntries)
                unregisterMethod.Invoke(null, new object[] { entryType });
        }

        private static IReadOnlyCollection<Type> GetRegisteredBlobEntryTypes(Type blobDbType)
        {
            var prop = blobDbType.GetProperty("RegisteredEntryTypes",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            if (prop == null) return Array.Empty<Type>();

            var value = prop.GetValue(null);
            if (value is IReadOnlyCollection<Type> col)
                return col;

            return Array.Empty<Type>();
        }

#if UNITY_EDITOR
        private static string FindBlobInEditor(string blobFileName)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets(
                Path.GetFileNameWithoutExtension(blobFileName));
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".blob"))
                    return path;
            }
            return null;
        }
#endif
    }
}
