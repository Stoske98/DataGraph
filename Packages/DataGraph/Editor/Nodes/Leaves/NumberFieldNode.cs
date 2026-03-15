using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Leaf node that reads a numeric value from a single cell.
    /// Number Type dropdown selects parsing mode: Int, Float, or Double.
    /// </summary>
    [Serializable]
    internal class NumberFieldNode : Node
    {
        [SerializeField] private string _fieldName = "value";
        [SerializeField] private string _column = "A";
        [SerializeField] private NumberType _numberType = NumberType.Int;

        public string FieldName => _fieldName;
        public string Column => _column;
        public NumberType NumberType => _numberType;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name").WithDefaultValue("value");
            context.AddOption<string>("Column").WithDisplayName("Column").WithDefaultValue("A");
            context.AddOption<NumberType>("NumberType").WithDisplayName("Number Type").WithDefaultValue(NumberType.Int);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent").WithDisplayName("Parent").Build();
        }
    }
}
