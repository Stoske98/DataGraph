using System;
using System.Collections.Generic;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Lightweight struct that wraps a registered database and exposes
    /// typed access methods. Only the methods matching the actual
    /// database kind (dictionary, array, or object) will succeed.
    /// Obtained via Database.Get&lt;T&gt;().
    /// </summary>
    public readonly struct DatabaseHandle<TValue>
    {
        private readonly object _database;

        internal DatabaseHandle(object database)
        {
            _database = database;
        }

        /// <summary>
        /// Access as int-keyed dictionary.
        /// Throws DatabaseKindMismatchException if the registered
        /// database is not a DictionaryDatabase with int keys.
        /// </summary>
        public DictionaryDatabase<int, TValue> AsDictionary()
        {
            if (_database is DictionaryDatabase<int, TValue> dict)
                return dict;
            throw new DatabaseKindMismatchException(
                typeof(TValue), "DictionaryDatabase<int>", _database.GetType());
        }

        /// <summary>
        /// Access as string-keyed dictionary.
        /// Throws DatabaseKindMismatchException if the registered
        /// database is not a DictionaryDatabase with string keys.
        /// </summary>
        public DictionaryDatabase<string, TValue> AsStringDictionary()
        {
            if (_database is DictionaryDatabase<string, TValue> dict)
                return dict;
            throw new DatabaseKindMismatchException(
                typeof(TValue), "DictionaryDatabase<string>", _database.GetType());
        }

        /// <summary>
        /// Access as ordered array collection.
        /// Throws DatabaseKindMismatchException if the registered
        /// database is not an ArrayDatabase.
        /// </summary>
        public ArrayDatabase<TValue> AsArray()
        {
            if (_database is ArrayDatabase<TValue> arr)
                return arr;
            throw new DatabaseKindMismatchException(
                typeof(TValue), "ArrayDatabase", _database.GetType());
        }

        /// <summary>
        /// Access as single-object store.
        /// Throws DatabaseKindMismatchException if the registered
        /// database is not an ObjectDatabase.
        /// </summary>
        public ObjectDatabase<TValue> AsObject()
        {
            if (_database is ObjectDatabase<TValue> obj)
                return obj;
            throw new DatabaseKindMismatchException(
                typeof(TValue), "ObjectDatabase", _database.GetType());
        }

        /// <summary>
        /// Shortcut — dictionary lookup by int key.
        /// </summary>
        public TValue GetById(int id) => AsDictionary().GetById(id);

        /// <summary>
        /// Shortcut — dictionary lookup by string key.
        /// </summary>
        public TValue GetById(string id) => AsStringDictionary().GetById(id);

        /// <summary>
        /// Shortcut — array lookup by zero-based index.
        /// </summary>
        public TValue GetByIndex(int index) => AsArray().GetByIndex(index);

        /// <summary>
        /// Shortcut — single object access.
        /// </summary>
        public TValue GetObject() => AsObject().Get();

        /// <summary>
        /// Returns all values regardless of database kind.
        /// </summary>
        public IEnumerable<TValue> GetAll()
        {
            return _database switch
            {
                DictionaryDatabase<int, TValue> dict => dict.GetAll(),
                DictionaryDatabase<string, TValue> dict => dict.GetAll(),
                ArrayDatabase<TValue> arr => arr.GetAll(),
                ObjectDatabase<TValue> obj => new[] { obj.Get() },
                _ => throw new DatabaseKindMismatchException(
                    typeof(TValue), "any known kind", _database.GetType())
            };
        }
    }
}
