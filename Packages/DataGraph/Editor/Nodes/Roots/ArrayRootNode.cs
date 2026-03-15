using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node representing an array root collection.
    /// </summary>
    [Serializable]
    internal class ArrayRootNode : Node
    {
        [SerializeField] private string _typeName = "Entry";

        public string TypeName => _typeName;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("TypeName").WithDisplayName("Type Name");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort<ChildConnection>("Children")
                .WithDisplayName("Children")
                .Build();
        }
    }
}
