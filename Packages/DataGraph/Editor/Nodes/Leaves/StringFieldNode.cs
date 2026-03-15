using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Leaf node that reads a string value from a single cell.
    /// </summary>
    [Serializable]
    internal class StringFieldNode : Node
    {
        [SerializeField] private string _fieldName = "text";
        [SerializeField] private string _column = "A";

        public string FieldName => _fieldName;
        public string Column => _column;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name").WithDefaultValue("text");
            context.AddOption<string>("Column").WithDisplayName("Column").WithDefaultValue("A");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
        }
    }
}
