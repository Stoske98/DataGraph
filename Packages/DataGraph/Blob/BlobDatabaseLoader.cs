#if DATAGRAPH_ENTITIES
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace DataGraph.Data
{
    /// <summary>
    /// Runtime component that loads .blob files and registers them
    /// in the BlobDatabase static registry on Awake.
    /// Each entry has a TextAsset (.bytes) or file path and the
    /// assembly-qualified type name of the generated BlobDatabaseBuilder.
    /// </summary>
    public class BlobDatabaseLoader : MonoBehaviour
    {
        [Serializable]
        public class BlobEntry
        {
            /// <summary>
            /// Path to the .blob file (relative to project or streaming assets).
            /// </summary>
            public string blobFilePath;

            /// <summary>
            /// Full type name of the generated BlobDatabaseBuilder
            /// (e.g. "DataGraph.Data.HeroBlobDatabaseBuilder, Assembly-CSharp").
            /// </summary>
            public string builderTypeName;
        }

        [SerializeField]
        private List<BlobEntry> _databases = new();

        private void Awake()
        {
            LoadAll();
        }

        private void OnDestroy()
        {
            BlobDatabase.Clear();
        }

        /// <summary>
        /// Loads all configured blob files and registers them.
        /// </summary>
        public void LoadAll()
        {
            BlobDatabase.Clear();

            foreach (var entry in _databases)
            {
                if (string.IsNullOrEmpty(entry.blobFilePath) ||
                    string.IsNullOrEmpty(entry.builderTypeName))
                    continue;

                LoadAndRegister(entry);
            }
        }

        private static void LoadAndRegister(BlobEntry entry)
        {
            var builderType = Type.GetType(entry.builderTypeName);
            if (builderType == null)
            {
                Debug.LogWarning($"[DataGraph] Builder type '{entry.builderTypeName}' not found.");
                return;
            }

            var loadMethod = builderType.GetMethod("Load",
                BindingFlags.Public | BindingFlags.Static);
            if (loadMethod == null)
            {
                Debug.LogWarning($"[DataGraph] Load method not found on '{entry.builderTypeName}'.");
                return;
            }

            try
            {
                var blobRef = loadMethod.Invoke(null, new object[] { entry.blobFilePath });

                var dbType = loadMethod.ReturnType.GetGenericArguments()[0];
                var registerMethod = typeof(BlobDatabase).GetMethod("Register")
                    .MakeGenericMethod(dbType);
                registerMethod.Invoke(null, new[] { blobRef });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DataGraph] Blob load failed: {ex.Message}");
            }
        }
    }
}
#endif
