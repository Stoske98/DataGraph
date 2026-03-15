using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Leaf node that reads a boolean value from a single cell.
    /// Accepts "true"/"false", "1"/"0", "yes"/"no" (case-insensitive).
    /// </summary>
    [Serializable]
    internal class BoolFieldNode : Node
    {
        [SerializeField] private string _fieldName = "isActive";
        [SerializeField] private string _column = "A";

        public string FieldName => _fieldName;
        public string Column => _column;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name").WithDefaultValue("isActive");
            context.AddOption<string>("Column").WithDisplayName("Column").WithDefaultValue("A");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
        }
    }
}
