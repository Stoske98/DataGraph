using System;
using UnityEngine;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Maps AssetType enum values to C# type name strings for code generation
    /// and to System.Type for asset loading during SO serialization.
    /// </summary>
    internal static class AssetTypeMapper
    {
        /// <summary>
        /// Returns the C# type name string used in generated code.
        /// </summary>
        public static string GetCSharpTypeName(AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Sprite => "Sprite",
                AssetType.Texture2D => "Texture2D",
                AssetType.AudioClip => "AudioClip",
                AssetType.GameObject => "GameObject",
                AssetType.Material => "Material",
                AssetType.AnimationClip => "AnimationClip",
                AssetType.RuntimeAnimatorController => "RuntimeAnimatorController",
                AssetType.ScriptableObject => "ScriptableObject",
                AssetType.Mesh => "Mesh",
                AssetType.Font => "Font",
                AssetType.TextAsset => "TextAsset",
                _ => "UnityEngine.Object"
            };
        }

        /// <summary>
        /// Returns the System.Type for AssetDatabase.LoadAssetAtPath.
        /// </summary>
        public static Type GetSystemType(AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Sprite => typeof(Sprite),
                AssetType.Texture2D => typeof(Texture2D),
                AssetType.AudioClip => typeof(AudioClip),
                AssetType.GameObject => typeof(GameObject),
                AssetType.Material => typeof(Material),
                AssetType.AnimationClip => typeof(AnimationClip),
                AssetType.RuntimeAnimatorController => typeof(RuntimeAnimatorController),
                AssetType.ScriptableObject => typeof(ScriptableObject),
                AssetType.Mesh => typeof(Mesh),
                AssetType.Font => typeof(Font),
                AssetType.TextAsset => typeof(TextAsset),
                _ => typeof(UnityEngine.Object)
            };
        }
    }
}
