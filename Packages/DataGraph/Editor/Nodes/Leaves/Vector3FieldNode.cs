using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Leaf node that reads a Vector3 from a single cell.
    /// Cell format: "x{sep}y{sep}z" (e.g. "1.5,2.0,3.0").
    /// </summary>
    [Serializable]
    internal class Vector3FieldNode : Node
    {
        [SerializeField] private string _fieldName;
        [SerializeField] private string _column;
        [SerializeField] private string _separator = ",";

        public string FieldName => _fieldName;
        public string Column => _column;
        public string Separator => _separator;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("Column").WithDisplayName("Column");
            context.AddOption<string>("Separator").WithDisplayName("Separator").WithDefaultValue(",");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
        }
    }
}
