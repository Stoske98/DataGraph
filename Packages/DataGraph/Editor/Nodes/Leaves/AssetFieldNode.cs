using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node for Unity asset reference fields.
    /// Reads an asset path from a cell and generates a typed
    /// asset reference in the output code.
    /// </summary>
    [Serializable]
    internal class AssetFieldNode : Node
    {
        [SerializeField] private string _fieldName = "asset";
        [SerializeField] private string _column = "A";
        [SerializeField] private AssetType _assetType = AssetType.Sprite;
        [SerializeField] private AssetLoadMethod _loadMethod = AssetLoadMethod.AssetDatabase;

        public string FieldName => _fieldName;
        public string Column => _column;
        public AssetType AssetType => _assetType;
        public AssetLoadMethod LoadMethod => _loadMethod;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("Column").WithDisplayName("Column");
            context.AddOption<AssetType>("AssetType").WithDisplayName("Asset Type");
            context.AddOption<AssetLoadMethod>("LoadMethod").WithDisplayName("Load Method");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent")
                .WithDisplayName("Parent")
                .Build();
        }
    }
}
