using UnityEngine;

namespace DataGraph.Data
{
    /// <summary>
    /// Abstract base ScriptableObject for all DataGraph generated database assets.
    /// Subclasses implement Register to self-register into the Database static registry.
    /// </summary>
    public abstract class DataGraphDatabaseAsset : ScriptableObject
    {
        /// <summary>
        /// The value type used as the registration key in Database.
        /// Returned so that DataGraphLoader can unregister only its own entries on destroy.
        /// </summary>
        public abstract Type EntryType { get; }

        /// <summary>
        /// Registers this database asset into the Database static registry.
        /// Called by DatabaseLoader on startup.
        /// </summary>
        public abstract void Register();
    }
}
