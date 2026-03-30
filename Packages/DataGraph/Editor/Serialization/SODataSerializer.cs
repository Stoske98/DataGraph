using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;
using UnityEditor;
using UnityEngine;

namespace DataGraph.Editor.Serialization
{
    /// <summary>
    /// Creates populated ScriptableObject .asset files from a ParsedDataTree.
    /// Uses reflection to instantiate generated types and populates them
    /// through the typed base class SetData methods.
    /// </summary>
    internal sealed class SODataSerializer
    {
        private Dictionary<string, UnityEngine.Object> _assetCache;

        /// <summary>
        /// Creates the database SO asset at the given path.
        /// Generated C# classes must be compiled before this runs.
        /// </summary>
        public Result<string> Serialize(ParsedDataTree tree, ParseableGraph graph, string outputPath)
        {
            if (tree?.Root == null)
                return Result<string>.Failure("Parsed data tree is null.");

            try
            {
                var dbTypeName = $"{graph.GraphName}Database";
                var dbType = FindType(dbTypeName);
                if (dbType == null)
                    return Result<string>.Failure(
                        $"Type '{dbTypeName}' not found. Run code generation first and wait for compilation.");

                var entryTypeName = GetRootTypeName(graph.Root);
                var entryType = FindType(entryTypeName);
                if (entryType == null)
                    return Result<string>.Failure($"Type '{entryTypeName}' not found.");

                _assetCache = PreloadAssets(tree.Root);
                var dbAsset = ScriptableObject.CreateInstance(dbType);

                switch (tree.Root)
                {
                    case ParsedDictionary dict:
                        PopulateDictionary(dbAsset, dict, entryType);
                        break;
                    case ParsedArray arr:
                        PopulateArray(dbAsset, arr, entryType);
                        break;
                    case ParsedObject obj:
                        PopulateObject(dbAsset, obj, entryType);
                        break;
                }

                var assetPath = Path.Combine(outputPath, $"{graph.GraphName}Database.asset");
                EnsureDirectory(assetPath);

                var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (existing != null)
                    AssetDatabase.DeleteAsset(assetPath);

                AssetDatabase.CreateAsset(dbAsset, assetPath);
                AssetDatabase.SaveAssets();

                return Result<string>.Success(assetPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"SO serialization failed: {ex.Message}");
            }
        }

        private void PopulateDictionary(ScriptableObject dbAsset, ParsedDictionary dict, Type entryType)
        {
            var setDataMethod = dbAsset.GetType().GetMethod("SetData",
                BindingFlags.Public | BindingFlags.Instance);
            if (setDataMethod == null) return;

            var keyType = dict.KeyTypeName == "int" ? typeof(int) : typeof(string);
            var keysList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(keyType));
            var entriesList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));

            foreach (var kvp in dict.Entries)
            {
                var key = ConvertValue(keyType, kvp.Key);
                keysList.Add(key);

                var entry = CreateAndPopulate(entryType, kvp.Value);
                entriesList.Add(entry);
            }

            setDataMethod.Invoke(dbAsset, new object[] { keysList, entriesList });
        }

        private void PopulateArray(ScriptableObject dbAsset, ParsedArray arr, Type entryType)
        {
            var setDataMethod = dbAsset.GetType().GetMethod("SetData",
                BindingFlags.Public | BindingFlags.Instance);
            if (setDataMethod == null) return;

            var entriesList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));

            foreach (var element in arr.Elements)
            {
                var entry = CreateAndPopulate(entryType, element);
                entriesList.Add(entry);
            }

            setDataMethod.Invoke(dbAsset, new object[] { entriesList });
        }

        private void PopulateObject(ScriptableObject dbAsset, ParsedObject obj, Type entryType)
        {
            var setDataMethod = dbAsset.GetType().GetMethod("SetData",
                BindingFlags.Public | BindingFlags.Instance);
            if (setDataMethod == null) return;

            var data = CreateAndPopulate(entryType, obj);
            setDataMethod.Invoke(dbAsset, new object[] { data });
        }

        private object CreateAndPopulate(Type type, ParsedNode node)
        {
            if (node is ParsedObject obj)
            {
                var instance = Activator.CreateInstance(type);

                foreach (var child in obj.Children)
                {
                    var field = type.GetField(child.FieldName,
                        BindingFlags.Public | BindingFlags.Instance);
                    if (field == null) continue;

                    var value = ResolveValue(field.FieldType, child);
                    if (value != null)
                        field.SetValue(instance, value);
                }

                return instance;
            }

            if (node is ParsedValue val)
                return ConvertValue(type, val.Value);

            return null;
        }

        private object ResolveValue(Type fieldType, ParsedNode node)
        {
            switch (node)
            {
                case ParsedAssetReference assetRef:
                    return ResolveAssetReference(assetRef);

                case ParsedValue val:
                    return ConvertValue(fieldType, val.Value);

                case ParsedObject obj:
                    return CreateAndPopulate(fieldType, obj);

                case ParsedArray arr:
                {
                    var elementType = fieldType.IsGenericType
                        ? fieldType.GetGenericArguments()[0]
                        : typeof(object);
                    var list = (IList)Activator.CreateInstance(
                        typeof(List<>).MakeGenericType(elementType));
                    foreach (var element in arr.Elements)
                    {
                        var item = element is ParsedValue pv
                            ? ConvertValue(elementType, pv.Value)
                            : CreateAndPopulate(elementType, element);
                        if (item != null) list.Add(item);
                    }
                    return list;
                }

                case ParsedDictionary dict:
                {
                    if (!fieldType.IsGenericType) return null;
                    var keyType = fieldType.GetGenericArguments()[0];
                    var valueType = fieldType.GetGenericArguments()[1];
                    var dictInstance = (IDictionary)Activator.CreateInstance(fieldType);
                    foreach (var kvp in dict.Entries)
                    {
                        var key = ConvertValue(keyType, kvp.Key);
                        var value = kvp.Value is ParsedValue pv
                            ? ConvertValue(valueType, pv.Value)
                            : CreateAndPopulate(valueType, kvp.Value);
                        if (key != null) dictInstance[key] = value;
                    }
                    return dictInstance;
                }

                default:
                    return null;
            }
        }

        private static object ConvertValue(Type targetType, object value)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;

            try
            {
                if (targetType == typeof(int)) return Convert.ToInt32(value);
                if (targetType == typeof(float)) return Convert.ToSingle(value);
                if (targetType == typeof(double)) return Convert.ToDouble(value);
                if (targetType == typeof(bool)) return Convert.ToBoolean(value);
                if (targetType == typeof(string)) return value.ToString();
                if (targetType.IsEnum) return Enum.Parse(targetType, value.ToString(), true);
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        /// <summary>
        /// Resolves an asset reference from the preloaded cache.
        /// </summary>
        private object ResolveAssetReference(ParsedAssetReference assetRef)
        {
            if (string.IsNullOrEmpty(assetRef.AssetPath))
                return null;

            _assetCache.TryGetValue(assetRef.AssetPath, out var cached);
            return cached;
        }

        /// <summary>
        /// Collects all unique asset paths from the parsed tree and batch-loads them.
        /// Each asset is loaded once regardless of how many entries reference it.
        /// </summary>
        private static Dictionary<string, UnityEngine.Object> PreloadAssets(ParsedNode root)
        {
            var refs = new List<ParsedAssetReference>();
            CollectAssetRefs(root, refs);

            var cache = new Dictionary<string, UnityEngine.Object>();
            foreach (var assetRef in refs)
            {
                if (string.IsNullOrEmpty(assetRef.AssetPath)) continue;
                if (cache.ContainsKey(assetRef.AssetPath)) continue;

                var loadType = AssetTypeMapper.GetSystemType(assetRef.AssetType);
                var asset = AssetDatabase.LoadAssetAtPath(assetRef.AssetPath, loadType);
                if (asset != null)
                    cache[assetRef.AssetPath] = asset;
            }

            return cache;
        }

        private static void CollectAssetRefs(ParsedNode node, List<ParsedAssetReference> refs)
        {
            switch (node)
            {
                case ParsedAssetReference assetRef:
                    refs.Add(assetRef);
                    break;
                case ParsedObject obj:
                    foreach (var child in obj.Children)
                        CollectAssetRefs(child, refs);
                    break;
                case ParsedArray arr:
                    foreach (var element in arr.Elements)
                        CollectAssetRefs(element, refs);
                    break;
                case ParsedDictionary dict:
                    foreach (var kvp in dict.Entries)
                        CollectAssetRefs(kvp.Value, refs);
                    break;
            }
        }

        private static string GetRootTypeName(ParseableNode root)
        {
            return root switch
            {
                ParseableDictionaryRoot dict => dict.TypeName,
                ParseableArrayRoot arr => arr.TypeName,
                ParseableObjectRoot obj => obj.TypeName,
                _ => throw new InvalidOperationException($"Unknown root: {root.GetType().Name}")
            };
        }

        private static Type FindType(string typeName)
        {
            var fullName = $"DataGraph.Data.{typeName}";

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
