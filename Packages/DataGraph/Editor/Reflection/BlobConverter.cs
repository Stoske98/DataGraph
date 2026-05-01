using System;
using System.Collections.Generic;
using System.Reflection;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Parsing;

namespace DataGraph.Editor.Reflection
{
    /// <summary>
    /// IValueConverter for Blob source structs. Asset references are
    /// kept as path strings (the BlobBuilder reads them later), arrays
    /// become T[], and dictionaries are written as parallel
    /// {field}Keys/{field}Values arrays.
    /// </summary>
    internal sealed class BlobConverter : IValueConverter
    {
        private const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        public object ConvertScalar(Type targetType, object value)
            => ScalarConverter.Convert(targetType, value);

        public object ConvertAsset(Type fieldType, ParsedAssetReference assetRef)
            => assetRef.AssetPath ?? "";

        public object PopulateNestedObject(ReflectionPopulator populator, Type fieldType, ParsedObject obj)
            => populator.Populate(fieldType, obj);

        public object PopulateArray(ReflectionPopulator populator, Type fieldType, ParsedArray arr)
        {
            var elementType = fieldType.GetElementType();
            if (elementType == null) return null;

            var array = Array.CreateInstance(elementType, arr.Elements.Count);
            for (int i = 0; i < arr.Elements.Count; i++)
            {
                object element = arr.Elements[i] switch
                {
                    ParsedValue val => ScalarConverter.Convert(elementType, val.Value),
                    ParsedObject obj => populator.Populate(elementType, obj),
                    _ => null
                };
                if (element != null) array.SetValue(element, i);
            }
            return array;
        }

        public void PopulateDictionaryFields(ReflectionPopulator populator,
            Type containerType, ParsedDictionary dict, object instance)
        {
            var keysField = populator.Cache.GetField(containerType, dict.FieldName + "Keys", PublicInstance);
            var valuesField = populator.Cache.GetField(containerType, dict.FieldName + "Values", PublicInstance);
            if (keysField == null || valuesField == null) return;

            var keyElementType = keysField.FieldType.GetElementType() ?? typeof(string);
            var valueElementType = valuesField.FieldType.GetElementType() ?? typeof(object);

            var keysList = new List<object>();
            var valuesList = new List<object>();

            foreach (var kvp in dict.Entries)
            {
                var key = ScalarConverter.Convert(keyElementType, kvp.Key);
                if (key != null) keysList.Add(key);

                object value = kvp.Value switch
                {
                    ParsedValue val => ScalarConverter.Convert(valueElementType, val.Value),
                    ParsedObject obj => populator.Populate(valueElementType, obj),
                    _ => null
                };
                valuesList.Add(value);
            }

            var keysArray = Array.CreateInstance(keyElementType, keysList.Count);
            for (int i = 0; i < keysList.Count; i++)
                keysArray.SetValue(keysList[i], i);
            keysField.SetValue(instance, keysArray);

            var valuesArray = Array.CreateInstance(valueElementType, valuesList.Count);
            for (int i = 0; i < valuesList.Count; i++)
                if (valuesList[i] != null) valuesArray.SetValue(valuesList[i], i);
            valuesField.SetValue(instance, valuesArray);
        }
    }
}
