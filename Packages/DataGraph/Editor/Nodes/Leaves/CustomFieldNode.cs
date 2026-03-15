using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node for typed leaf fields (string, int, float, bool, Vector2/3, Color, Enum).
    /// </summary>
    [Serializable]
    internal class CustomFieldNode : Node
    {
        [SerializeField] private string _fieldName = "value";
        [SerializeField] private string _column = "A";
        [SerializeField] private FieldValueType _valueType = FieldValueType.String;
        [SerializeField] private string _separator = ",";
        [SerializeField] private string _format = "hex";
        [SerializeField] private string _enumTypeAssemblyQualifiedName = "";

        public string FieldName => _fieldName;
        public string Column => _column;
        public FieldValueType ValueType => _valueType;
        public string Separator => _separator;
        public string Format => _format;

        public Type ResolveEnumType()
        {
            if (string.IsNullOrEmpty(_enumTypeAssemblyQualifiedName))
                return null;
            return Type.GetType(_enumTypeAssemblyQualifiedName);
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("Column").WithDisplayName("Column");
            context.AddOption<FieldValueType>("ValueType").WithDisplayName("Value Type");
            context.AddOption<string>("Separator").WithDisplayName("Separator");
            context.AddOption<string>("Format").WithDisplayName("Format");
            context.AddOption<string>("EnumType").WithDisplayName("Enum Type");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent")
                .WithDisplayName("Parent")
                .Build();
        }
    }
}
