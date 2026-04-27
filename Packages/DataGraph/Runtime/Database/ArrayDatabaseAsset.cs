using System;
using System.Collections.Generic;
using UnityEngine;
using DataGraph.Runtime;

namespace DataGraph.Data
{
    /// <summary>
    /// Generic ScriptableObject for sequential data collections.
    /// Entries are accessed by zero-based index.
    /// </summary>
    public class ArrayDatabaseAsset<TValue> : DataGraphDatabaseAsset
    {
        /// <summary>
        /// The value type used as the registration key in Database.
        /// </summary>
        public override Type EntryType => typeof(TValue);
        [SerializeField] private List<TValue> _entries = new();

        /// <summary>
        /// Number of entries.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Retrieves the entry at the given index.
        /// </summary>
        public TValue GetByIndex(int index)
        {
            if (index < 0 || index >= _entries.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _entries[index];
        }

        /// <summary>
        /// Returns all entries in order.
        /// </summary>
        public IReadOnlyList<TValue> GetAll() => _entries;

        /// <summary>
        /// Populates the asset from external data. Used by SODataSerializer.
        /// </summary>
        public void SetData(List<TValue> entries)
        {
            _entries = entries;
        }

        public override void Register()
        {
            Database.Register(new ArrayDatabase<TValue>(_entries));
        }
    }
}
