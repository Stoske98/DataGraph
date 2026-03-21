using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node representing a nested dictionary field (vertical only).
    /// </summary>
    [Serializable]
    internal class DictionaryFieldNode : Node
    {
        [SerializeField] private string _fieldName;
        [SerializeField] private string _typeName;
        [SerializeField] private string _keyColumn;
        [SerializeField] private KeyType _keyType = KeyType.String;

        public string FieldName => _fieldName;
        public string TypeName => _typeName;
        public string KeyColumn => _keyColumn;
        public KeyType KeyType => _keyType;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("TypeName").WithDisplayName("Type Name");
            context.AddOption<string>("KeyColumn").WithDisplayName("Key Column");
            context.AddOption<KeyType>("KeyType").WithDisplayName("Key Type");
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
