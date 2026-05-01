using System;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Reflection
{
    /// <summary>
    /// Format-specific bridge used by <see cref="ReflectionPopulator"/>.
    /// Each output format (SO / Blob / Quantum) implements this to handle
    /// scalar conversion, nested object/array population, and dictionary
    /// shape (native Dictionary vs parallel arrays vs parallel lists).
    /// The walk over a ParsedObject's children is identical across formats
    /// and lives in ReflectionPopulator.
    /// </summary>
    internal interface IValueConverter
    {
        /// <summary>
        /// Converts a scalar parsed value to the target field type. For
        /// Quantum this is also where FP / FPVector wrapping happens.
        /// May return null to signal "leave field at default".
        /// </summary>
        object ConvertScalar(Type targetType, object value);

        /// <summary>
        /// Converts a parsed asset reference (path + AssetType) to the
        /// target field type. SO returns the loaded UnityEngine.Object,
        /// Blob returns the path string, Quantum returns either path or
        /// loaded asset depending on field type.
        /// </summary>
        object ConvertAsset(Type fieldType, ParsedAssetReference assetRef);

        /// <summary>
        /// Populates a nested object instance from a ParsedObject. The
        /// converter reuses ReflectionPopulator internally for the recursive
        /// walk; this hook exists so format-specific instance creation
        /// (e.g. Quantum's empty constructor + id field) can wrap the call.
        /// </summary>
        object PopulateNestedObject(ReflectionPopulator populator, Type fieldType, ParsedObject obj);

        /// <summary>
        /// Populates a sequential collection (array or list) from a
        /// ParsedArray. SO/Quantum produce a List&lt;T&gt;, Blob produces
        /// a T[].
        /// </summary>
        object PopulateArray(ReflectionPopulator populator, Type fieldType, ParsedArray arr);

        /// <summary>
        /// Writes a ParsedDictionary into the container's representation.
        /// SO sets the {fieldName}Keys / {fieldName}Values lists, Blob
        /// sets {fieldName}Keys / {fieldName}Values arrays, Quantum sets
        /// the same as lists. The populator handles GetField for the
        /// surrounding instance; this method owns the parallel-collection
        /// shape.
        /// </summary>
        void PopulateDictionaryFields(ReflectionPopulator populator,
            Type containerType, ParsedDictionary dict, object instance);
    }
}
