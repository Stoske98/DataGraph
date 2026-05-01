using System;
using System.Reflection;
using DataGraph.Editor.Domain;
using UnityEngine;

namespace DataGraph.Editor.Reflection
{
    /// <summary>
    /// Walks a ParsedObject's children and populates the corresponding
    /// fields on a managed instance via reflection. Format-specific
    /// behaviour (scalar conversion, nested array shape, dictionary
    /// representation) is delegated to an <see cref="IValueConverter"/>.
    /// Carries a per-parse <see cref="Reflection.ReflectionCache"/> so
    /// repeated GetField calls during the walk stay O(1).
    /// </summary>
    internal sealed class ReflectionPopulator
    {
        private const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        public ReflectionCache Cache { get; }
        public IValueConverter Converter { get; }

        public ReflectionPopulator(ReflectionCache cache, IValueConverter converter)
        {
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            Converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        /// <summary>
        /// Creates a new instance of <paramref name="type"/>. If
        /// <paramref name="node"/> is a ParsedObject, populates the
        /// instance's fields from its children. Other node kinds return
        /// the freshly constructed (empty) instance — this matches the
        /// pre-T19 helpers, which silently no-op'd for non-object nodes.
        /// </summary>
        public object Populate(Type type, ParsedNode node)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var instance = Activator.CreateInstance(type);
            if (node is ParsedObject obj)
                PopulateInto(type, obj, instance);
            return instance;
        }

        /// <summary>
        /// Populates an existing instance from <paramref name="obj"/>'s
        /// children. Used when the caller already constructed the instance
        /// (e.g. Quantum entry with pre-set id field).
        /// </summary>
        public void PopulateInto(Type type, ParsedObject obj, object instance)
        {
            foreach (var child in obj.Children)
            {
                if (child is ParsedDictionary childDict)
                {
                    Converter.PopulateDictionaryFields(this, type, childDict, instance);
                    continue;
                }

                var field = Cache.GetField(type, child.FieldName, PublicInstance);
                if (field == null)
                {
                    Debug.LogWarning(
                        $"DataGraph: field '{child.FieldName}' not found on type " +
                        $"'{type.FullName}'. Skipping. Re-run code generation " +
                        "if the schema changed.");
                    continue;
                }

                object value = child switch
                {
                    ParsedValue val => Converter.ConvertScalar(field.FieldType, val.Value),
                    ParsedAssetReference assetRef => Converter.ConvertAsset(field.FieldType, assetRef),
                    ParsedObject childObj => Converter.PopulateNestedObject(this, field.FieldType, childObj),
                    ParsedArray childArr => Converter.PopulateArray(this, field.FieldType, childArr),
                    _ => null
                };

                if (value != null)
                    field.SetValue(instance, value);
            }
        }
    }
}
