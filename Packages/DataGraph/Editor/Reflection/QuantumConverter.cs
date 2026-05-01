using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Parsing;
using UnityEditor;

namespace DataGraph.Editor.Reflection
{
    /// <summary>
    /// IValueConverter for Photon Quantum AssetObject entries.
    /// Float/double/Vector2/Vector3 values are wrapped in FP/FPVector2/
    /// FPVector3 to keep simulation-side code deterministic; the wrapping
    /// goes through reflection because Quantum types are not referenced
    /// at compile time. Strings, colors, and asset fields stay native
    /// (they sit inside #if QUANTUM_UNITY blocks in the generated entry
    /// class). Arrays are emitted as List&lt;T&gt;, dictionaries as
    /// parallel List&lt;K&gt;/List&lt;V&gt;.
    /// </summary>
    internal sealed class QuantumConverter : IValueConverter
    {
        private const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        private static readonly BindingFlags PublicStatic =
            BindingFlags.Public | BindingFlags.Static;

        private static readonly Type[] FloatParam = { typeof(float) };

        private readonly ReflectionCache _cache;

        public QuantumConverter(ReflectionCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public object ConvertScalar(Type targetType, object value)
        {
            if (value == null) return null;

            var typeName = targetType.FullName;

            if (typeName == "Photon.Deterministic.FP")
            {
                var fromFloat = _cache.GetMethod(targetType, "FromFloat_UNSAFE", PublicStatic, FloatParam);
                if (fromFloat != null)
                    return fromFloat.Invoke(null, new object[] { System.Convert.ToSingle(value) });
            }

            if (typeName == "Photon.Deterministic.FPVector2" && value is UnityEngine.Vector2 v2)
            {
                var fpType = _cache.FindType("Photon.Deterministic.FP");
                var fromFloat = fpType == null ? null
                    : _cache.GetMethod(fpType, "FromFloat_UNSAFE", PublicStatic, FloatParam);
                if (fromFloat != null)
                {
                    var x = fromFloat.Invoke(null, new object[] { v2.x });
                    var y = fromFloat.Invoke(null, new object[] { v2.y });
                    var ctor = targetType.GetConstructor(new[] { fpType, fpType });
                    if (ctor != null) return ctor.Invoke(new[] { x, y });
                }
            }

            if (typeName == "Photon.Deterministic.FPVector3" && value is UnityEngine.Vector3 v3)
            {
                var fpType = _cache.FindType("Photon.Deterministic.FP");
                var fromFloat = fpType == null ? null
                    : _cache.GetMethod(fpType, "FromFloat_UNSAFE", PublicStatic, FloatParam);
                if (fromFloat != null)
                {
                    var x = fromFloat.Invoke(null, new object[] { v3.x });
                    var y = fromFloat.Invoke(null, new object[] { v3.y });
                    var z = fromFloat.Invoke(null, new object[] { v3.z });
                    var ctor = targetType.GetConstructor(new[] { fpType, fpType, fpType });
                    if (ctor != null) return ctor.Invoke(new[] { x, y, z });
                }
            }

            return ScalarConverter.Convert(targetType, value);
        }

        public object ConvertAsset(Type fieldType, ParsedAssetReference assetRef)
        {
            if (string.IsNullOrEmpty(assetRef.AssetPath)) return null;
            if (fieldType == typeof(string)) return assetRef.AssetPath;

            var loadType = AssetTypeMapper.GetSystemType(assetRef.AssetType);
            return AssetDatabase.LoadAssetAtPath(assetRef.AssetPath, loadType);
        }

        public object PopulateNestedObject(ReflectionPopulator populator, Type fieldType, ParsedObject obj)
            => populator.Populate(fieldType, obj);

        public object PopulateArray(ReflectionPopulator populator, Type fieldType, ParsedArray arr)
        {
            if (!fieldType.IsGenericType) return null;

            var elementType = fieldType.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(fieldType);

            foreach (var element in arr.Elements)
            {
                object item = element switch
                {
                    ParsedValue val => ConvertScalar(elementType, val.Value),
                    ParsedObject obj => populator.Populate(elementType, obj),
                    _ => null
                };
                if (item != null) list.Add(item);
            }

            return list;
        }

        public void PopulateDictionaryFields(ReflectionPopulator populator,
            Type containerType, ParsedDictionary dict, object instance)
        {
            var keysField = populator.Cache.GetField(containerType, dict.FieldName + "Keys");
            var valuesField = populator.Cache.GetField(containerType, dict.FieldName + "Values");
            if (keysField == null || valuesField == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"DataGraph (Quantum): expected fields '{dict.FieldName}Keys' and '{dict.FieldName}Values' " +
                    $"on type '{containerType.FullName}' for dictionary serialization. Skipping. " +
                    "Re-run code generation if the schema changed.");
                return;
            }

            var keysList = (IList)keysField.GetValue(instance);
            var valuesList = (IList)valuesField.GetValue(instance);
            if (keysList == null || valuesList == null) return;

            var valueElementType = valuesField.FieldType.GetGenericArguments()[0];
            var keyElementType = keysField.FieldType.GetGenericArguments()[0];

            foreach (var kvp in dict.Entries)
            {
                var key = ScalarConverter.Convert(keyElementType, kvp.Key);
                if (key != null) keysList.Add(key);

                object value = kvp.Value switch
                {
                    ParsedValue val => ConvertScalar(valueElementType, val.Value),
                    ParsedObject obj => populator.Populate(valueElementType, obj),
                    _ => null
                };
                if (value != null) valuesList.Add(value);
            }
        }
    }
}
