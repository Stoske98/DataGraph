using System;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Base class for single-object game data stores.
    /// Used for global configurations where exactly one
    /// data instance exists (e.g. game settings, balance config).
    /// Populated by DataGraph during parsing.
    /// </summary>
    public class ObjectDatabase<TValue>
    {
        private readonly TValue _data;

        public ObjectDatabase(TValue data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Returns the single stored object.
        /// </summary>
        public TValue Get()
        {
            return _data;
        }
    }
}
