namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Immutable domain representation of an asset field within ParseableGraph.
    /// Always resolves through AssetDatabase.LoadAssetAtPath at editor time.
    /// </summary>
    internal sealed class ParseableAssetField : ParseableNode
    {
        public ParseableAssetField(
            string fieldName,
            string column,
            AssetType assetType)
            : base(fieldName, System.Array.Empty<ParseableNode>())
        {
            Column = column;
            AssetType = assetType;
        }

        public string Column { get; }
        public AssetType AssetType { get; }
    }
}
