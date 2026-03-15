using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node representing a nested object field.
    /// </summary>
    [Serializable]
    internal class ObjectFieldNode : Node
    {
        [SerializeField] private string _fieldName = "data";
        [SerializeField] private string _typeName = "Data";

        public string FieldName => _fieldName;
        public string TypeName => _typeName;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("TypeName").WithDisplayName("Type Name");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent")
                .WithDisplayName("Parent")
                .Build();
            context.AddOutputPort<ChildConnection>("Children")
                .WithDisplayName("Children")
                .Build();
        }
    }
}
