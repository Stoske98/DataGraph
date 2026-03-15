using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node representing an object root (single-object store).
    /// </summary>
    [Serializable]
    internal class ObjectRootNode : Node
    {
        [SerializeField] private string _typeName = "Config";

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
