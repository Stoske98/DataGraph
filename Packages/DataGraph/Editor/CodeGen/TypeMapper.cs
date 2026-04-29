using System;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.CodeGen
{
    /// <summary>
    /// Maps domain types (FieldValueType, KeyType) to the concrete type-name
    /// strings used by each output format. Centralized here so all generators
    /// stay in sync; default cases throw <see cref="InvalidOperationException"/>
    /// instead of returning a silent fallback so missing mappings fail loudly.
    /// </summary>
    internal static class TypeMapper
    {
        /// <summary>
        /// C# type name used in ScriptableObject / JSON output classes.
        /// </summary>
        public static string GetSOType(FieldValueType valueType, Type enumType = null)
        {
            return valueType switch
            {
                FieldValueType.String => "string",
                FieldValueType.Int => "int",
                FieldValueType.Float => "float",
                FieldValueType.Double => "double",
                FieldValueType.Bool => "bool",
                FieldValueType.Vector2 => "Vector2",
                FieldValueType.Vector3 => "Vector3",
                FieldValueType.Color => "Color",
                FieldValueType.Enum => enumType?.Name ?? "int",
                _ => throw new InvalidOperationException(
                    $"GetSOType: unsupported FieldValueType {valueType}")
            };
        }

        /// <summary>
        /// Type name used in Blob output structs. Strings become BlobString;
        /// vectors and colors keep their Unity types because BlobAssetReference
        /// supports the layout. Enums are written as int (their underlying type).
        /// </summary>
        public static string GetBlobType(FieldValueType valueType)
        {
            return valueType switch
            {
                FieldValueType.String => "BlobString",
                FieldValueType.Int => "int",
                FieldValueType.Float => "float",
                FieldValueType.Double => "double",
                FieldValueType.Bool => "bool",
                FieldValueType.Vector2 => "UnityEngine.Vector2",
                FieldValueType.Vector3 => "UnityEngine.Vector3",
                FieldValueType.Color => "UnityEngine.Color",
                FieldValueType.Enum => "int",
                _ => throw new InvalidOperationException(
                    $"GetBlobType: unsupported FieldValueType {valueType}")
            };
        }

        /// <summary>
        /// Photon Quantum simulation-side type. Floats and doubles are mapped
        /// to FP for determinism, vectors to FPVector2/3. Throws for value
        /// types that cannot exist in the simulation layer (String, Color,
        /// Enum) — those go through <see cref="GetQuantumViewType"/>.
        /// </summary>
        public static string GetQuantumSimType(FieldValueType valueType)
        {
            return valueType switch
            {
                FieldValueType.Int => "int",
                FieldValueType.Float => "FP",
                FieldValueType.Double => "FP",
                FieldValueType.Bool => "bool",
                FieldValueType.Vector2 => "FPVector2",
                FieldValueType.Vector3 => "FPVector3",
                _ => throw new InvalidOperationException(
                    $"GetQuantumSimType: FieldValueType.{valueType} is not " +
                    "simulation-safe; route it through the view layer instead.")
            };
        }

        /// <summary>
        /// Photon Quantum view-side type. Only types that cannot live in the
        /// deterministic simulation (String, Color) are valid here. Throws for
        /// simulation-safe types so callers do not silently route the wrong
        /// kind of value through the view layer.
        /// </summary>
        public static string GetQuantumViewType(FieldValueType valueType)
        {
            return valueType switch
            {
                FieldValueType.String => "string",
                FieldValueType.Color => "UnityEngine.Color",
                _ => throw new InvalidOperationException(
                    $"GetQuantumViewType: FieldValueType.{valueType} is " +
                    "simulation-safe; use GetQuantumSimType instead.")
            };
        }

        /// <summary>
        /// Primitive JSON Schema type ("string", "integer", "number",
        /// "boolean"). Vector2/Vector3/Color have nested object schemas
        /// (x/y/z, r/g/b/a) — callers must detect those types upfront and
        /// emit the structured schema rather than calling this helper.
        /// </summary>
        public static string GetJsonSchemaType(FieldValueType valueType)
        {
            return valueType switch
            {
                FieldValueType.String => "string",
                FieldValueType.Int => "integer",
                FieldValueType.Float => "number",
                FieldValueType.Double => "number",
                FieldValueType.Bool => "boolean",
                FieldValueType.Enum => "string",
                FieldValueType.Vector2 or FieldValueType.Vector3 or FieldValueType.Color =>
                    throw new InvalidOperationException(
                        $"GetJsonSchemaType: {valueType} requires a structured " +
                        "schema (object with named coordinates), not a primitive type."),
                _ => throw new InvalidOperationException(
                    $"GetJsonSchemaType: unsupported FieldValueType {valueType}")
            };
        }

        /// <summary>
        /// Backwards-compatible alias for <see cref="GetSOType"/>.
        /// </summary>
        public static string GetCSharpTypeName(FieldValueType valueType, Type enumType = null) =>
            GetSOType(valueType, enumType);

        /// <summary>
        /// C# type name for a dictionary key type.
        /// </summary>
        public static string GetKeyTypeName(KeyType keyType)
        {
            return keyType switch
            {
                KeyType.Int => "int",
                KeyType.String => "string",
                _ => throw new InvalidOperationException(
                    $"GetKeyTypeName: unsupported KeyType {keyType}")
            };
        }
    }
}
