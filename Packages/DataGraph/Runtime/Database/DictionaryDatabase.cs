using System;
using System.Collections.Generic;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Base class for key-based game data collections.
    /// Stores entries accessible by a typed key (int or string).
    /// Populated by DataGraph during parsing — not intended for
    /// manual construction in user code.
    /// </summary>
    public class DictionaryDatabase<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _entries;

        public DictionaryDatabase(Dictionary<TKey, TValue> entries)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        /// <summary>
        /// Number of entries in this collection.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Retrieves the entry with the given key.
        /// Throws KeyNotFoundException if the key does not exist.
        /// </summary>
        public TValue GetById(TKey id)
        {
            if (_entries.TryGetValue(id, out var value))
                return value;
            throw new KeyNotFoundException(
                $"No {typeof(TValue).Name} found with key '{id}'.");
        }

        /// <summary>
        /// Attempts to retrieve the entry with the given key.
        /// Returns true if found, false otherwise.
        /// </summary>
        public bool TryGetById(TKey id, out TValue value)
        {
            return _entries.TryGetValue(id, out value);
        }

        /// <summary>
        /// Returns all entries in this collection.
        /// </summary>
        public IEnumerable<TValue> GetAll()
        {
            return _entries.Values;
        }

        /// <summary>
        /// Returns all keys in this collection.
        /// </summary>
        public IEnumerable<TKey> GetAllKeys()
        {
            return _entries.Keys;
        }

        /// <summary>
        /// Returns true if the given key exists in this collection.
        /// </summary>
        public bool ContainsKey(TKey id)
        {
            return _entries.ContainsKey(id);
        }
    }
}
