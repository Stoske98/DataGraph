namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// A parsed asset reference. Holds the raw path/address string from
    /// the cell along with metadata about asset type and load method
    /// needed by serializers to resolve the actual Unity asset.
    /// </summary>
    internal sealed class ParsedAssetReference : ParsedNode
    {
        public ParsedAssetReference(
            string fieldName,
            string assetPath,
            AssetType assetType,
            AssetLoadMethod loadMethod)
            : base(fieldName)
        {
            AssetPath = assetPath;
            AssetType = assetType;
            LoadMethod = loadMethod;
        }

        /// <summary>
        /// Raw path or address string read from the cell.
        /// For AssetDatabase: project-relative path (e.g. "Assets/Art/sword.png").
        /// For Addressables: addressable key string.
        /// </summary>
        public string AssetPath { get; }

        /// <summary>
        /// Unity asset type for this reference.
        /// </summary>
        public AssetType AssetType { get; }

        /// <summary>
        /// How this asset should be resolved.
        /// </summary>
        public AssetLoadMethod LoadMethod { get; }
    }
}
