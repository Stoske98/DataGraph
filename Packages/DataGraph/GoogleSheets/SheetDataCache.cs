using System;
using System.Collections.Generic;
using DataGraph.Runtime;

namespace DataGraph.GoogleSheets
{
    /// <summary>
    /// In-memory cache for fetched RawTableData.
    /// JSON Preview re-parses from this cache on every graph change
    /// instead of hitting the API.
    /// </summary>
    internal sealed class SheetDataCache
    {
        private readonly Dictionary<string, CacheEntry> _entries = new();

        public RawTableData Get(string sheetId)
        {
            if (string.IsNullOrEmpty(sheetId))
                return null;
            return _entries.TryGetValue(sheetId, out var entry) ? entry.Data : null;
        }

        public void Set(string sheetId, RawTableData data)
        {
            if (!string.IsNullOrEmpty(sheetId))
                _entries[sheetId] = new CacheEntry(data, DateTime.UtcNow);
        }

        public void Remove(string sheetId)
        {
            if (!string.IsNullOrEmpty(sheetId))
                _entries.Remove(sheetId);
        }

        public void Clear() => _entries.Clear();

        public int Count => _entries.Count;

        private readonly struct CacheEntry
        {
            public readonly RawTableData Data;
            public readonly DateTime FetchTime;

            public CacheEntry(RawTableData data, DateTime fetchTime)
            {
                Data = data;
                FetchTime = fetchTime;
            }
        }
    }
}
