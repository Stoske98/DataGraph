namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Predefined Unity asset types supported by AssetFieldNode.
    /// Determines the C# type used in generated code and the
    /// type passed to AssetDatabase.LoadAssetAtPath during SO serialization.
    /// </summary>
    internal enum AssetType
    {
        Sprite,
        Texture2D,
        AudioClip,
        GameObject,
        Material,
        AnimationClip,
        RuntimeAnimatorController,
        ScriptableObject,
        Mesh,
        Font,
        TextAsset
    }
}
