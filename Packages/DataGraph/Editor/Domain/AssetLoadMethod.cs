namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Determines how a referenced Unity asset is loaded at runtime.
    /// </summary>
    internal enum AssetLoadMethod
    {
        AssetDatabase,
        Addressables,
        Resources
    }
}
