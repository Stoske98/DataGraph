#if DATAGRAPH_ENTITIES
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace DataGraph.Data
{
    /// <summary>
    /// Non-generic interface for blob database handles.
    /// Enables storage in a single dictionary keyed by entry type.
    /// </summary>
    public interface IBlobDatabaseHandle<TEntry> where TEntry : unmanaged
    {
        ref TEntry GetById(int key);
        ref TEntry GetByStringId(string key);
        ref TEntry GetByIndex(int index);
        ref TEntry GetObject();
    }

    /// <summary>
    /// Typed handle for accessing blob database entries.
    /// Mirrors DatabaseHandle pattern from SO API.
    /// </summary>
    public sealed class BlobDatabaseHandle<TEntry, TDatabase> : IBlobDatabaseHandle<TEntry>
        where TEntry : unmanaged
        where TDatabase : unmanaged
    {
        public delegate ref TEntry IntLookupDelegate(ref TDatabase db, int key);
        public delegate ref TEntry StringLookupDelegate(ref TDatabase db, string key);
        public delegate ref TEntry IndexDelegate(ref TDatabase db, int index);
        public delegate ref TEntry ObjectDelegate(ref TDatabase db);

        private BlobAssetReference<TDatabase> _blobRef;
        private readonly IntLookupDelegate _getById;
        private readonly StringLookupDelegate _getByStringId;
        private readonly IndexDelegate _getByIndex;
        private readonly ObjectDelegate _getObject;

        public BlobDatabaseHandle(
            BlobAssetReference<TDatabase> blobRef,
            IntLookupDelegate getById = null,
            StringLookupDelegate getByStringId = null,
            IndexDelegate getByIndex = null,
            ObjectDelegate getObject = null)
        {
            _blobRef = blobRef;
            _getById = getById;
            _getByStringId = getByStringId;
            _getByIndex = getByIndex;
            _getObject = getObject;
        }

        public ref TEntry GetById(int key) => ref _getById(ref _blobRef.Value, key);
        public ref TEntry GetByStringId(string key) => ref _getByStringId(ref _blobRef.Value, key);
        public ref TEntry GetByIndex(int index) => ref _getByIndex(ref _blobRef.Value, index);
        public ref TEntry GetObject() => ref _getObject(ref _blobRef.Value);
    }

    /// <summary>
    /// Central static registry for blob database assets.
    /// Keyed by entry type for uniform API.
    /// Usage: ref var hero = ref BlobDatabase.Get&lt;HeroBlob&gt;().GetById(1);
    /// </summary>
    public static class BlobDatabase
    {
        private static readonly Dictionary<Type, object> _handles = new();
        private static readonly Dictionary<Type, object> _rawRefs = new();
        private static readonly Dictionary<Type, Type> _entryToDatabaseType = new();

        /// <summary>
        /// Registers a blob database with a typed handle.
        /// Called by generated Register method.
        /// </summary>
        public static void Register<TEntry, TDatabase>(
            BlobAssetReference<TDatabase> blobRef,
            BlobDatabaseHandle<TEntry, TDatabase> handle)
            where TEntry : unmanaged
            where TDatabase : unmanaged
        {
            _handles[typeof(TEntry)] = handle;
            _rawRefs[typeof(TDatabase)] = blobRef;
            _entryToDatabaseType[typeof(TEntry)] = typeof(TDatabase);
        }

        /// <summary>
        /// Registers a raw BlobAssetReference by database type.
        /// Used by DataGraphLoader via reflection.
        /// </summary>
        public static void RegisterRaw<TDatabase>(BlobAssetReference<TDatabase> blobRef)
            where TDatabase : unmanaged
        {
            _rawRefs[typeof(TDatabase)] = blobRef;
        }

        /// <summary>
        /// Retrieves the handle for the given entry type.
        /// Usage: BlobDatabase.Get&lt;HeroBlob&gt;().GetById(1)
        /// </summary>
        public static IBlobDatabaseHandle<TEntry> Get<TEntry>() where TEntry : unmanaged
        {
            if (!_handles.TryGetValue(typeof(TEntry), out var handle))
                throw new InvalidOperationException(
                    $"No blob database registered for entry type '{typeof(TEntry).Name}'.");

            return (IBlobDatabaseHandle<TEntry>)handle;
        }

        /// <summary>
        /// Returns true if a blob database is registered for the given entry type.
        /// </summary>
        public static bool IsRegistered<TEntry>() where TEntry : unmanaged
        {
            return _handles.ContainsKey(typeof(TEntry));
        }

        /// <summary>
        /// All currently registered entry types. Used by DataGraphLoader
        /// to snapshot registrations before and after a blob load
        /// so it can track and later unregister only its own entries.
        /// </summary>
        internal static IReadOnlyCollection<Type> RegisteredEntryTypes => _handles.Keys;

        /// <summary>
        /// Removes the blob database registered for the given entry type.
        /// Disposes the underlying BlobAssetReference if found.
        /// Does nothing if the entry type is not registered.
        /// </summary>
        public static void Unregister(Type entryType)
        {
            _handles.Remove(entryType);
            if (_entryToDatabaseType.TryGetValue(entryType, out var dbType))
            {
                if (_rawRefs.TryGetValue(dbType, out var raw) && raw is IDisposable disposable)
                    disposable.Dispose();
                _rawRefs.Remove(dbType);
                _entryToDatabaseType.Remove(entryType);
            }
        }

        /// <summary>
        /// Removes all registered blob databases and disposes references.
        /// </summary>
        public static void Clear()
        {
            foreach (var kvp in _rawRefs)
            {
                if (kvp.Value is IDisposable disposable)
                    disposable.Dispose();
            }
            _handles.Clear();
            _rawRefs.Clear();
            _entryToDatabaseType.Clear();
        }
    }
}
#endif
