using System;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Thrown when attempting to access a database type
    /// that has not been registered via Database.Register.
    /// </summary>
    public sealed class DatabaseNotFoundException : InvalidOperationException
    {
        public DatabaseNotFoundException(Type valueType)
            : base($"No database registered for type '{valueType.Name}'. " +
                   "Ensure the data has been parsed and registered before access.")
        {
            ValueType = valueType;
        }

        /// <summary>
        /// The value type that was requested but not found.
        /// </summary>
        public Type ValueType { get; }
    }

    /// <summary>
    /// Thrown when attempting to access a database through the wrong
    /// kind accessor (e.g. calling GetById on an ArrayDatabase).
    /// </summary>
    public sealed class DatabaseKindMismatchException : InvalidOperationException
    {
        public DatabaseKindMismatchException(
            Type valueType,
            string expectedKind,
            Type actualType)
            : base($"Database for '{valueType.Name}' is {actualType.Name}, " +
                   $"but was accessed as {expectedKind}.")
        {
            ValueType = valueType;
            ExpectedKind = expectedKind;
            ActualType = actualType;
        }

        /// <summary>
        /// The value type that was requested.
        /// </summary>
        public Type ValueType { get; }

        /// <summary>
        /// The kind of database the caller expected.
        /// </summary>
        public string ExpectedKind { get; }

        /// <summary>
        /// The actual type of the registered database.
        /// </summary>
        public Type ActualType { get; }
    }
}
