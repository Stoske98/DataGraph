using System;
using System.Collections.Generic;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Base class for sequential game data collections.
    /// Stores entries accessible by zero-based index.
    /// Populated by DataGraph during parsing — not intended for
    /// manual construction in user code.
    /// </summary>
    public class ArrayDatabase<TValue>
    {
        private readonly IReadOnlyList<TValue> _entries;

        public ArrayDatabase(IReadOnlyList<TValue> entries)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        /// <summary>
        /// Number of entries in this collection.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Retrieves the entry at the given zero-based index.
        /// Throws ArgumentOutOfRangeException if the index is invalid.
        /// </summary>
        public TValue GetByIndex(int index)
        {
            if (index < 0 || index >= _entries.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index {index} is out of range for {typeof(TValue).Name} " +
                    $"collection with {_entries.Count} entries.");
            return _entries[index];
        }

        /// <summary>
        /// Returns all entries in insertion order.
        /// </summary>
        public IEnumerable<TValue> GetAll()
        {
            return _entries;
        }
    }
}
