using System;
using System.Collections.Generic;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Central static registry for parsed game data.
    /// Provides type-safe access to database instances from anywhere in code.
    /// Part of DataGraph.Runtime — available in production builds
    /// without editor dependencies.
    /// </summary>
    public static class Database
    {
        private static readonly Dictionary<Type, object> _databases = new();

        /// <summary>
        /// Registers an int-keyed dictionary database for the given value type.
        /// </summary>
        public static void Register<TValue>(DictionaryDatabase<int, TValue> db)
        {
            _databases[typeof(TValue)] = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Registers a string-keyed dictionary database for the given value type.
        /// </summary>
        public static void Register<TValue>(DictionaryDatabase<string, TValue> db)
        {
            _databases[typeof(TValue)] = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Registers an array database for the given value type.
        /// </summary>
        public static void Register<TValue>(ArrayDatabase<TValue> db)
        {
            _databases[typeof(TValue)] = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Registers an object database for the given value type.
        /// </summary>
        public static void Register<TValue>(ObjectDatabase<TValue> db)
        {
            _databases[typeof(TValue)] = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Retrieves the database registered for the given value type.
        /// Returns a DatabaseHandle that provides access methods
        /// matching the database kind.
        /// Throws DatabaseNotFoundException if no database is registered.
        /// </summary>
        public static DatabaseHandle<TValue> Get<TValue>()
        {
            if (!_databases.TryGetValue(typeof(TValue), out var db))
                throw new DatabaseNotFoundException(typeof(TValue));
            return new DatabaseHandle<TValue>(db);
        }

        /// <summary>
        /// Returns true if a database is registered for the given value type.
        /// </summary>
        public static bool IsRegistered<TValue>()
        {
            return _databases.ContainsKey(typeof(TValue));
        }

        /// <summary>
        /// Removes all registered databases. Typically called during
        /// cleanup or before re-parsing.
        /// </summary>
        public static void Clear()
        {
            _databases.Clear();
        }
    }
}
