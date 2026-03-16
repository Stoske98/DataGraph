using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Structural node that reads array elements from a single cell,
    /// split by a separator character. Only supports primitive child types.
    /// </summary>
    [Serializable]
    internal class HorizontalArrayFieldNode : Node
    {
        [SerializeField] private string _fieldName = "tags";
        [SerializeField] private string _separator = ",";

        public string FieldName => _fieldName;
        public string Separator => _separator;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name").WithDefaultValue("tags");
            context.AddOption<string>("Separator").WithDisplayName("Separator").WithDefaultValue(",");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
            context.AddOutputPort<ChildConnection>("Children").WithDisplayName("Children").Build();
        }
    }
}
