using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Parsing;

namespace DataGraph.Editor.Reflection
{
    /// <summary>
    /// IValueConverter for ScriptableObject entry classes. Asset
    /// references are resolved against a preloaded asset cache so each
    /// path hits AssetDatabase exactly once. Arrays become List&lt;T&gt;.
    /// Dictionary fields follow the same parallel-list pattern as the
    /// generated entry types ({field}Keys / {field}Values), matching the
    /// shape produced by CodeGenerator.WriteDictionaryFieldAsLists.
    /// </summary>
    internal sealed class SOConverter : IValueConverter
    {
        private const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        private readonly Dictionary<string, UnityEngine.Object> _assetCache;

        public SOConverter(Dictionary<string, UnityEngine.Object> assetCache)
        {
            _assetCache = assetCache ?? new Dictionary<string, UnityEngine.Object>();
        }

        public object ConvertScalar(Type targetType, object value)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;

            try
            {
                if (targetType == typeof(int)) return System.Convert.ToInt32(value);
                if (targetType == typeof(float)) return System.Convert.ToSingle(value);
                if (targetType == typeof(double)) return System.Convert.ToDouble(value);
                if (targetType == typeof(bool)) return System.Convert.ToBoolean(value);
                if (targetType == typeof(string)) return value.ToString();
                if (targetType.IsEnum) return EnumParser.Parse(targetType, value.ToString());
                return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        public object ConvertAsset(Type fieldType, ParsedAssetReference assetRef)
        {
            if (string.IsNullOrEmpty(assetRef.AssetPath)) return null;
            _assetCache.TryGetValue(assetRef.AssetPath, out var cached);
            return cached;
        }

        public object PopulateNestedObject(ReflectionPopulator populator, Type fieldType, ParsedObject obj)
            => populator.Populate(fieldType, obj);

        public object PopulateArray(ReflectionPopulator populator, Type fieldType, ParsedArray arr)
        {
            var elementType = fieldType.IsGenericType
                ? fieldType.GetGenericArguments()[0]
                : typeof(object);
            var list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(elementType));

            foreach (var element in arr.Elements)
            {
                var item = element is ParsedValue pv
                    ? ConvertScalar(elementType, pv.Value)
                    : populator.Populate(elementType, (ParsedObject)element);
                if (item != null) list.Add(item);
            }

            return list;
        }

        public void PopulateDictionaryFields(ReflectionPopulator populator,
            Type containerType, ParsedDictionary dict, object instance)
        {
            // Generated entry classes expose dict fields as parallel
            // List<K>/List<V> (see CodeGenerator.WriteDictionaryFieldAsLists);
            // we set those lists directly. The runtime property on the
            // generated class lazily reconstructs the Dictionary on access.
            var keysField = populator.Cache.GetField(containerType, dict.FieldName + "Keys", PublicInstance);
            var valuesField = populator.Cache.GetField(containerType, dict.FieldName + "Values", PublicInstance);
            if (keysField == null || valuesField == null) return;

            var keysList = (IList)keysField.GetValue(instance);
            var valuesList = (IList)valuesField.GetValue(instance);
            if (keysList == null || valuesList == null) return;

            var keyElementType = keysField.FieldType.GetGenericArguments()[0];
            var valueElementType = valuesField.FieldType.GetGenericArguments()[0];

            foreach (var kvp in dict.Entries)
            {
                var key = ConvertScalar(keyElementType, kvp.Key);
                if (key != null) keysList.Add(key);

                object value = kvp.Value switch
                {
                    ParsedValue val => ConvertScalar(valueElementType, val.Value),
                    ParsedObject obj => populator.Populate(valueElementType, obj),
                    _ => null
                };
                valuesList.Add(value);
            }
        }
    }
}
