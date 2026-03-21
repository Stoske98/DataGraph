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
            AssetType assetType,
            AssetLoadMethod loadMethod)
            : base(fieldName, Array.Empty<ParseableNode>())
        {
            Column = column;
            AssetType = assetType;
            LoadMethod = loadMethod;
        }

        /// <summary>
        /// Column letter from which the asset path or key is read.
        /// </summary>
        public string Column { get; }

        /// <summary>
        /// Unity asset type for this field.
        /// </summary>
        public AssetType AssetType { get; }

        /// <summary>
        /// How this asset is resolved — direct reference or addressable string.
        /// </summary>
        public AssetLoadMethod LoadMethod { get; }
    }
}
