using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Structural node that reads array elements spread across multiple rows.
    /// Uses Index Column as tracker — sequential values indicate elements
    /// belonging to the current parent object.
    /// </summary>
    [Serializable]
    internal class VerticalArrayFieldNode : Node
    {
        [SerializeField] private string _fieldName;
        [SerializeField] private string _typeName;
        [SerializeField] private string _indexColumn;

        public string FieldName => _fieldName;
        public string TypeName => _typeName;
        public string IndexColumn => _indexColumn;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("TypeName").WithDisplayName("Type Name");
            context.AddOption<string>("IndexColumn").WithDisplayName("Index Column");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
            context.AddOutputPort<ChildConnection>("Children").WithDisplayName("Children").Build();
        }
    }
}
