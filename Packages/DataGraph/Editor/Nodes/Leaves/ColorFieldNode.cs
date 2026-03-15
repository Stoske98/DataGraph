using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Leaf node that reads a Color from a single cell.
    /// Supports hex (#RRGGBB, #RRGGBBAA) and rgba (r,g,b,a) formats.
    /// </summary>
    [Serializable]
    internal class ColorFieldNode : Node
    {
        [SerializeField] private string _fieldName = "color";
        [SerializeField] private string _column = "A";
        [SerializeField] private string _format = "hex";

        public string FieldName => _fieldName;
        public string Column => _column;
        public string Format => _format;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name").WithDefaultValue("color");
            context.AddOption<string>("Column").WithDisplayName("Column").WithDefaultValue("A");
            context.AddOption<string>("Format").WithDisplayName("Format").WithDefaultValue("hex");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
        }
    }
}
