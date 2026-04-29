using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DataGraph.Editor;
using DataGraph.Editor.Adapter;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class GraphValidatorTests
    {
        private static DataGraphAsset MakeAsset()
        {
            return ScriptableObject.CreateInstance<DataGraphAsset>();
        }

        private static SerializedNode AddNode(DataGraphAsset asset, string typeName, string guid)
        {
            var node = new SerializedNode { Guid = guid, TypeName = typeName };
            asset.Nodes.Add(node);
            return node;
        }

        private static void AddEdge(DataGraphAsset asset, string parentGuid, string childGuid)
        {
            asset.Edges.Add(new SerializedEdge
            {
                OutputNodeGuid = parentGuid,
                InputNodeGuid = childGuid
            });
        }

        [Test]
        public void Validate_NoRoot_ProducesError()
        {
            var asset = MakeAsset();
            AddNode(asset, NodeTypeRegistry.Types.Object, "n1");

            var result = new GraphValidator().Validate(asset);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Root")));
        }

        [Test]
        public void Validate_TwoRoots_ProducesError()
        {
            var asset = MakeAsset();
            AddNode(asset, NodeTypeRegistry.Types.Root, "r1");
            AddNode(asset, NodeTypeRegistry.Types.Root, "r2");

            var result = new GraphValidator().Validate(asset);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("2 root nodes")));
        }

        [Test]
        public void Validate_ObjectMissingTypeName_ProducesError()
        {
            var asset = MakeAsset();
            AddNode(asset, NodeTypeRegistry.Types.Root, "r1");
            AddNode(asset, NodeTypeRegistry.Types.Object, "obj1");
            AddEdge(asset, "r1", "obj1");

            var result = new GraphValidator().Validate(asset);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Type Name")));
        }

        [Test]
        public void Validate_LeafUnderObject_RequiresFieldName()
        {
            var asset = MakeAsset();
            AddNode(asset, NodeTypeRegistry.Types.Root, "r1");
            var obj = AddNode(asset, NodeTypeRegistry.Types.Object, "obj1");
            obj.SetProperty("TypeName", "Hero");
            AddNode(asset, NodeTypeRegistry.Types.StringField, "leaf1");
            AddEdge(asset, "r1", "obj1");
            AddEdge(asset, "obj1", "leaf1");

            var result = new GraphValidator().Validate(asset);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Field Name")));
        }

        [Test]
        public void Validate_LeafWithFieldName_NoFieldNameError()
        {
            var asset = MakeAsset();
            AddNode(asset, NodeTypeRegistry.Types.Root, "r1");
            var obj = AddNode(asset, NodeTypeRegistry.Types.Object, "obj1");
            obj.SetProperty("TypeName", "Hero");
            var leaf = AddNode(asset, NodeTypeRegistry.Types.StringField, "leaf1");
            leaf.SetProperty("FieldName", "name");
            AddEdge(asset, "r1", "obj1");
            AddEdge(asset, "obj1", "leaf1");

            var result = new GraphValidator().Validate(asset);

            Assert.IsFalse(result.Errors.Any(e => e.Contains("Field Name")));
        }

        [Test]
        public void Validate_DisconnectedNonRootNode_ProducesWarning()
        {
            var asset = MakeAsset();
            AddNode(asset, NodeTypeRegistry.Types.Root, "r1");
            var obj = AddNode(asset, NodeTypeRegistry.Types.Object, "orphan");
            obj.SetProperty("TypeName", "Hero");
            // No edge connects "orphan" to anything

            var result = new GraphValidator().Validate(asset);

            Assert.IsTrue(result.Warnings.Any(w => w.Contains("not connected")));
        }

        [Test]
        public void Validate_LeafNotUnderObject_DoesNotRequireFieldName()
        {
            // VerticalArray as a single-leaf collection: child does not need FieldName
            var asset = MakeAsset();
            AddNode(asset, NodeTypeRegistry.Types.Root, "r1");
            AddNode(asset, NodeTypeRegistry.Types.VerticalArray, "arr");
            AddNode(asset, NodeTypeRegistry.Types.StringField, "leaf");
            AddEdge(asset, "r1", "arr");
            AddEdge(asset, "arr", "leaf");

            var result = new GraphValidator().Validate(asset);

            Assert.IsFalse(result.Errors.Any(e => e.Contains("Field Name")));
        }
    }
}
