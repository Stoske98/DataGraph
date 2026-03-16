using System.Collections.Generic;
using UnityEngine;
using DataGraph.Runtime;

namespace DataGraph.Data
{
    /// <summary>
    /// Generic ScriptableObject for key-value data collections.
    /// Stores data as parallel lists (Unity serializable) and builds
    /// a Dictionary on deserialization for fast runtime access.
    /// </summary>
    public class DictionaryDatabaseAsset<TKey, TValue> : DataGraphDatabaseAsset, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> _keys = new();
        [SerializeField] private List<TValue> _values = new();

        private Dictionary<TKey, TValue> _dictionary;

        /// <summary>
        /// Runtime dictionary built from serialized lists.
        /// </summary>
        public Dictionary<TKey, TValue> Dictionary
        {
            get
            {
                if (_dictionary == null)
                    RebuildDictionary();
                return _dictionary;
            }
        }

        /// <summary>
        /// Number of entries.
        /// </summary>
        public int Count => _keys.Count;

        /// <summary>
        /// Retrieves the entry with the given key.
        /// </summary>
        public TValue GetById(TKey id)
        {
            if (Dictionary.TryGetValue(id, out var value))
                return value;
            throw new KeyNotFoundException(
                $"No {typeof(TValue).Name} found with key '{id}'.");
        }

        /// <summary>
        /// Attempts to retrieve the entry with the given key.
        /// </summary>
        public bool TryGetById(TKey id, out TValue value)
        {
            return Dictionary.TryGetValue(id, out value);
        }

        /// <summary>
        /// Returns all values.
        /// </summary>
        public IEnumerable<TValue> GetAll() => Dictionary.Values;

        /// <summary>
        /// Returns all keys.
        /// </summary>
        public IEnumerable<TKey> GetAllKeys() => Dictionary.Keys;

        /// <summary>
        /// Populates the asset from external data. Used by SODataSerializer.
        /// </summary>
        public void SetData(List<TKey> keys, List<TValue> values)
        {
            _keys = keys;
            _values = values;
            RebuildDictionary();
        }

        public override void Register()
        {
            Database.Register(new DictionaryDatabase<TKey, TValue>(Dictionary));
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            RebuildDictionary();
        }

        private void RebuildDictionary()
        {
            _dictionary = new Dictionary<TKey, TValue>();
            var count = Mathf.Min(_keys.Count, _values.Count);
            for (int i = 0; i < count; i++)
                _dictionary[_keys[i]] = _values[i];
        }
    }
}
