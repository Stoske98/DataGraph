using System.Collections.Generic;
using UnityEngine;

namespace DataGraph.Data
{
    /// <summary>
    /// Runtime component that registers DataGraph database assets
    /// into the Database static registry on Awake.
    /// Attach to a GameObject and assign database SO assets in the Inspector.
    /// </summary>
    public class DatabaseLoader : MonoBehaviour
    {
        [SerializeField]
        private List<DataGraphDatabaseAsset> _databases = new();

        private void Awake()
        {
            LoadAll();
        }

        /// <summary>
        /// Clears the registry and registers all assigned database assets.
        /// </summary>
        public void LoadAll()
        {
            Runtime.Database.Clear();

            foreach (var db in _databases)
            {
                if (db == null) continue;
                db.Register();
            }
        }
    }
}
