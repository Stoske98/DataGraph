using System;

namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Domain representation of an asset reference field.
    /// Reads an asset path or key from a cell and configures
    /// typed asset loading in generated code.
    /// </summary>
    internal sealed class ParseableAssetField : ParseableNode
    {
        public ParseableAssetField(
            string fieldName,
            string column,
            string assetTypeName,
            AssetLoadMethod loadMethod)
            : base(fieldName, Array.Empty<ParseableNode>())
        {
            Column = column;
            AssetTypeName = assetTypeName;
            LoadMethod = loadMethod;
        }

        /// <summary>
        /// Column letter from which the asset path or key is read.
        /// </summary>
        public string Column { get; }

        /// <summary>
        /// Name of the Unity asset type (e.g. "Sprite", "AudioClip").
        /// </summary>
        public string AssetTypeName { get; }

        /// <summary>
        /// How this asset is loaded at runtime.
        /// </summary>
        public AssetLoadMethod LoadMethod { get; }
    }
}
