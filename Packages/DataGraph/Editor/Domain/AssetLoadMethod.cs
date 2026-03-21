namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Determines how a referenced Unity asset is resolved.
    /// AssetDatabase: direct reference serialized on SO field.
    /// Addressables: string address stored for runtime async loading.
    /// </summary>
    internal enum AssetLoadMethod
    {
        AssetDatabase,
        Addressables
    }
}
