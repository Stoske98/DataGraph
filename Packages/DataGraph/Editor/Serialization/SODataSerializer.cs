using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DataGraph.Editor.Domain;
using DataGraph.Editor.IO;
using DataGraph.Editor.Reflection;
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
                var dbType = TypeFinder.FindGenerated(dbTypeName);
                if (dbType == null)
                    return Result<string>.Failure(
                        $"Type '{dbTypeName}' not found. Run code generation first and wait for compilation.");

                var entryTypeName = RootTypeResolver.GetTypeName(graph.Root);
                var entryType = TypeFinder.FindGenerated(entryTypeName);
                if (entryType == null)
                    return Result<string>.Failure($"Type '{entryTypeName}' not found.");

                var assetCache = PreloadAssets(tree.Root);
                var converter = new SOConverter(assetCache);
                var populator = new ReflectionPopulator(new ReflectionCache(), converter);
                var dbAsset = ScriptableObject.CreateInstance(dbType);

                switch (tree.Root)
                {
                    case ParsedDictionary dict:
                        PopulateDictionary(dbAsset, dict, entryType, populator, converter);
                        break;
                    case ParsedArray arr:
                        PopulateArray(dbAsset, arr, entryType, populator);
                        break;
                    case ParsedObject obj:
                        PopulateObject(dbAsset, obj, entryType, populator);
                        break;
                }

                var assetPath = Path.Combine(outputPath, $"{graph.GraphName}Database.asset");
                PathUtilities.EnsureDirectory(assetPath);

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

        private static MethodInfo GetSetData(ScriptableObject dbAsset, string kindLabel)
        {
            var setDataMethod = dbAsset.GetType().GetMethod("SetData",
                BindingFlags.Public | BindingFlags.Instance);
            if (setDataMethod == null)
            {
                Debug.LogWarning(
                    $"DataGraph (SO): 'SetData' method not found on '{dbAsset.GetType().FullName}'. " +
                    $"{kindLabel} database will be empty. Re-run code generation.");
            }
            return setDataMethod;
        }

        private static void PopulateDictionary(ScriptableObject dbAsset, ParsedDictionary dict, Type entryType,
            ReflectionPopulator populator, SOConverter converter)
        {
            var setDataMethod = GetSetData(dbAsset, "Dictionary");
            if (setDataMethod == null) return;

            var keyType = dict.KeyTypeName == "int" ? typeof(int) : typeof(string);
            var keysList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(keyType));
            var entriesList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));

            foreach (var kvp in dict.Entries)
            {
                keysList.Add(converter.ConvertScalar(keyType, kvp.Key));
                entriesList.Add(populator.Populate(entryType, kvp.Value));
            }

            setDataMethod.Invoke(dbAsset, new object[] { keysList, entriesList });
        }

        private static void PopulateArray(ScriptableObject dbAsset, ParsedArray arr, Type entryType,
            ReflectionPopulator populator)
        {
            var setDataMethod = GetSetData(dbAsset, "Array");
            if (setDataMethod == null) return;

            var entriesList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
            foreach (var element in arr.Elements)
                entriesList.Add(populator.Populate(entryType, element));

            setDataMethod.Invoke(dbAsset, new object[] { entriesList });
        }

        private static void PopulateObject(ScriptableObject dbAsset, ParsedObject obj, Type entryType,
            ReflectionPopulator populator)
        {
            var setDataMethod = GetSetData(dbAsset, "Object");
            if (setDataMethod == null) return;

            setDataMethod.Invoke(dbAsset, new object[] { populator.Populate(entryType, obj) });
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

    }
}
