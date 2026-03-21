using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node for Unity asset reference fields.
    /// </summary>
    [Serializable]
    internal class AssetFieldNode : Node
    {
        [SerializeField] private string _fieldName;
        [SerializeField] private string _column;
        [SerializeField] private string _assetTypeName;
        [SerializeField] private AssetLoadMethod _loadMethod = AssetLoadMethod.Resources;

        public string FieldName => _fieldName;
        public string Column => _column;
        public string AssetTypeName => _assetTypeName;
        public AssetLoadMethod LoadMethod => _loadMethod;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("Column").WithDisplayName("Column");
            context.AddOption<string>("AssetTypeName").WithDisplayName("Asset Type");
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
