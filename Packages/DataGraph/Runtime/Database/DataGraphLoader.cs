using System;
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
    /// </summary>
    public sealed class DataGraphLoader : MonoBehaviour
    {
        [SerializeField]
        private DataGraphRegistry _registry;

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
            Database.Clear();
            TryClearBlobDatabase();
        }

        private void LoadSODatabases()
        {
            foreach (var asset in _registry.SOAssets)
            {
                if (asset == null) continue;
                asset.Register();
            }
        }

        private void LoadBlobDatabases()
        {
            foreach (var entry in _registry.BlobEntries)
            {
                if (string.IsNullOrEmpty(entry.builderTypeName) ||
                    string.IsNullOrEmpty(entry.blobFileName))
                    continue;

                LoadBlob(entry);
            }
        }

        private static void LoadBlob(DataGraphRegistry.BlobEntry entry)
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
                if (registerMethod != null)
                {
                    registerMethod.Invoke(null, new[] { blobRef });
                }
                else
                {
                    Debug.LogWarning($"[DataGraph] Register method not found on '{entry.builderTypeName}'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DataGraph] Blob load failed for '{entry.blobFileName}': {ex.Message}");
            }
        }

        private static void TryClearBlobDatabase()
        {
            var blobDbType = Type.GetType("DataGraph.Data.BlobDatabase, DataGraph.Blob");
            if (blobDbType == null) return;

            var clearMethod = blobDbType.GetMethod("Clear");
            clearMethod?.Invoke(null, null);
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
