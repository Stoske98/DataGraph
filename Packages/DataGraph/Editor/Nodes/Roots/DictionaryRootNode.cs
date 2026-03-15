using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using DataGraph.Editor.Domain;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// GTK node representing a dictionary root collection.
    /// </summary>
    [Serializable]
    internal class DictionaryRootNode : Node
    {
        [SerializeField] private string _typeName = "Item";
        [SerializeField] private string _keyColumn = "A";
        [SerializeField] private KeyType _keyType = KeyType.Int;

        public string TypeName => _typeName;
        public string KeyColumn => _keyColumn;
        public KeyType KeyType => _keyType;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("TypeName").WithDisplayName("Type Name");
            context.AddOption<string>("KeyColumn").WithDisplayName("Key Column");
            context.AddOption<KeyType>("KeyType").WithDisplayName("Key Type");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort<ChildConnection>("Children")
                .WithDisplayName("Children")
                .Build();
        }
    }
}
