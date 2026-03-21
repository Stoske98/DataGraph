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
        [SerializeField] private string _fieldName;
        [SerializeField] private string _column;
        [SerializeField] private string _format;

        public string FieldName => _fieldName;
        public string Column => _column;
        public string Format => _format;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("Column").WithDisplayName("Column");
            context.AddOption<string>("Format").WithDisplayName("Format");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
        }
    }
}
