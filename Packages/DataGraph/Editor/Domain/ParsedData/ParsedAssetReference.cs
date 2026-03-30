namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// A parsed asset reference. Holds the raw path string from
    /// the cell along with the asset type needed by serializers
    /// to resolve the actual Unity asset via AssetDatabase.
    /// </summary>
    internal sealed class ParsedAssetReference : ParsedNode
    {
        public ParsedAssetReference(
            string fieldName,
            string assetPath,
            AssetType assetType)
            : base(fieldName)
        {
            AssetPath = assetPath;
            AssetType = assetType;
        }

        /// <summary>
        /// Project-relative path read from the cell (e.g. "Assets/Art/sword.png").
        /// </summary>
        public string AssetPath { get; }

        /// <summary>
        /// Unity asset type for this reference.
        /// </summary>
        public AssetType AssetType { get; }
    }
}
