using System;
using System.Collections.Generic;
using DataGraph.Data;
using UnityEngine;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Central registry that holds references to all generated database assets.
    /// Automatically maintained by DataGraph editor tools during Parse and Create Assets.
    /// Used by DataGraphLoader at runtime to discover and load all databases.
    /// </summary>
    [CreateAssetMenu(menuName = "DataGraph/Registry", fileName = "DataGraphRegistry")]
    public sealed class DataGraphRegistry : ScriptableObject
    {
        [Serializable]
        public class BlobEntry
        {
            /// <summary>
            /// Assembly-qualified type name of the generated BlobDatabaseBuilder.
            /// </summary>
            public string builderTypeName;

            /// <summary>
            /// Blob file name (without path). At runtime, loaded from StreamingAssets/DataGraph/.
            /// </summary>
            public string blobFileName;
        }

        [SerializeField]
        private List<DataGraphDatabaseAsset> _soAssets = new();

        [SerializeField]
        private List<BlobEntry> _blobEntries = new();

        /// <summary>
        /// All registered SO database assets.
        /// </summary>
        public IReadOnlyList<DataGraphDatabaseAsset> SOAssets => _soAssets;

        /// <summary>
        /// All registered Blob database entries.
        /// </summary>
        public IReadOnlyList<BlobEntry> BlobEntries => _blobEntries;

        /// <summary>
        /// Adds or updates an SO database asset reference.
        /// </summary>
        public void RegisterSO(DataGraphDatabaseAsset asset)
        {
            if (asset == null) return;
            if (!_soAssets.Contains(asset))
                _soAssets.Add(asset);
        }

        /// <summary>
        /// Adds or updates a Blob database entry.
        /// </summary>
        public void RegisterBlob(string builderTypeName, string blobFileName)
        {
            if (string.IsNullOrEmpty(builderTypeName) || string.IsNullOrEmpty(blobFileName))
                return;

            for (int i = 0; i < _blobEntries.Count; i++)
            {
                if (_blobEntries[i].blobFileName == blobFileName)
                {
                    _blobEntries[i].builderTypeName = builderTypeName;
                    return;
                }
            }

            _blobEntries.Add(new BlobEntry
            {
                builderTypeName = builderTypeName,
                blobFileName = blobFileName
            });
        }

        /// <summary>
        /// Removes null SO references and stale Blob entries.
        /// </summary>
        public void CleanUp()
        {
            _soAssets.RemoveAll(a => a == null);
            _blobEntries.RemoveAll(e =>
                string.IsNullOrEmpty(e.builderTypeName) ||
                string.IsNullOrEmpty(e.blobFileName));
        }

        /// <summary>
        /// Removes an SO database asset by matching its name.
        /// </summary>
        public void UnregisterSO(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return;
            _soAssets.RemoveAll(a => a != null && a.name == assetName + "Database");
        }

        /// <summary>
        /// Removes a Blob entry by matching its blob file name.
        /// </summary>
        public void UnregisterBlob(string graphName)
        {
            if (string.IsNullOrEmpty(graphName)) return;
            var blobFileName = graphName + ".blob";
            _blobEntries.RemoveAll(e => e.blobFileName == blobFileName);
        }
    }
}
