using UnityEngine;
using DataGraph.Runtime;

namespace DataGraph.Data
{
    /// <summary>
    /// Generic ScriptableObject for single-object data stores.
    /// Used for global configurations where exactly one data instance exists.
    /// </summary>
    public class ObjectDatabaseAsset<TValue> : DataGraphDatabaseAsset
    {
        [SerializeField] private TValue _data;

        /// <summary>
        /// Returns the stored data object.
        /// </summary>
        public TValue Data => _data;

        /// <summary>
        /// Populates the asset from external data. Used by SODataSerializer.
        /// </summary>
        public void SetData(TValue data)
        {
            _data = data;
        }

        public override void Register()
        {
            Database.Register(new ObjectDatabase<TValue>(_data));
        }
    }
}
