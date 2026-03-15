using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node representing an array field with Horizontal/Vertical mode.
    /// </summary>
    [Serializable]
    internal class ArrayFieldNode : Node
    {
        [SerializeField] private string _fieldName = "items";
        [SerializeField] private string _typeName = "Element";
        [SerializeField] private ArrayMode _arrayMode = ArrayMode.Vertical;
        [SerializeField] private string _indexColumn = "B";
        [SerializeField] private string _separator = ",";

        public string FieldName => _fieldName;
        public string TypeName => _typeName;
        public ArrayMode ArrayMode => _arrayMode;
        public string IndexColumn => _indexColumn;
        public string Separator => _separator;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("TypeName").WithDisplayName("Type Name");
            context.AddOption<ArrayMode>("ArrayMode").WithDisplayName("Array Mode");
            context.AddOption<string>("IndexColumn").WithDisplayName("Index Column");
            context.AddOption<string>("Separator").WithDisplayName("Separator");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<ChildConnection>("Parent")
                .WithDisplayName("Parent")
                .Build();
            context.AddOutputPort<ChildConnection>("Children")
                .WithDisplayName("Children")
                .Build();
        }
    }
}
