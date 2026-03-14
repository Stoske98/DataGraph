using System;
using System.Linq;
using NUnit.Framework;
using DataGraph.Editor.Domain;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class ParseableGraphTests
    {
        [Test]
        public void Builder_DictionaryRoot_BuildsCorrectGraph()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithSheetId("sheet123")
                .WithHeaderRowOffset(1)
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                    new ParseableCustomField("damage", "C", FieldValueType.Int),
                }))
                .Build();

            Assert.AreEqual("Items", graph.GraphName);
            Assert.AreEqual("sheet123", graph.SheetId);
            Assert.AreEqual(1, graph.HeaderRowOffset);
            Assert.IsInstanceOf<ParseableDictionaryRoot>(graph.Root);

            var root = (ParseableDictionaryRoot)graph.Root;
            Assert.AreEqual("Item", root.TypeName);
            Assert.AreEqual("A", root.KeyColumn);
            Assert.AreEqual(KeyType.Int, root.KeyType);
            Assert.AreEqual(2, root.Children.Count);
            Assert.IsNull(root.FieldName);
        }

        [Test]
        public void Builder_ArrayRoot_BuildsCorrectGraph()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Levels")
                .WithRoot(new ParseableArrayRoot("Level", new ParseableNode[]
                {
                    new ParseableCustomField("difficulty", "A", FieldValueType.Int),
                }))
                .Build();

            Assert.IsInstanceOf<ParseableArrayRoot>(graph.Root);
            var root = (ParseableArrayRoot)graph.Root;
            Assert.AreEqual("Level", root.TypeName);
            Assert.AreEqual(1, root.Children.Count);
        }

        [Test]
        public void Builder_ObjectRoot_BuildsCorrectGraph()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Config")
                .WithRoot(new ParseableObjectRoot("GameConfig", new ParseableNode[]
                {
                    new ParseableCustomField("maxPlayers", "A", FieldValueType.Int),
                    new ParseableCustomField("gameName", "B", FieldValueType.String),
                }))
                .Build();

            Assert.IsInstanceOf<ParseableObjectRoot>(graph.Root);
            var root = (ParseableObjectRoot)graph.Root;
            Assert.AreEqual("GameConfig", root.TypeName);
            Assert.AreEqual(2, root.Children.Count);
        }

        [Test]
        public void Builder_NoRoot_Throws()
        {
            var builder = new ParseableGraphBuilder().WithGraphName("Empty");

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void AllNodes_ContainsAllNodesInTree()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                    new ParseableObjectField("stats", "ItemStats", new ParseableNode[]
                    {
                        new ParseableCustomField("hp", "C", FieldValueType.Int),
                        new ParseableCustomField("atk", "D", FieldValueType.Float),
                    }),
                }))
                .Build();

            Assert.AreEqual(5, graph.AllNodes.Count);
        }

        [Test]
        public void NestedVerticalArray_StructureIsCorrect()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                    new ParseableArrayField("skills", "Skill", ArrayMode.Vertical, "C", null, new ParseableNode[]
                    {
                        new ParseableCustomField("skillId", "D", FieldValueType.Int),
                        new ParseableArrayField("levels", "Level", ArrayMode.Vertical, "E", null, new ParseableNode[]
                        {
                            new ParseableCustomField("req", "F", FieldValueType.Int),
                            new ParseableCustomField("power", "G", FieldValueType.Int),
                        }),
                    }),
                }))
                .Build();

            Assert.AreEqual(7, graph.AllNodes.Count);

            var root = (ParseableDictionaryRoot)graph.Root;
            var skills = (ParseableArrayField)root.Children[1];
            Assert.AreEqual("skills", skills.FieldName);
            Assert.AreEqual(ArrayMode.Vertical, skills.Mode);
            Assert.AreEqual("C", skills.IndexColumn);

            var levels = (ParseableArrayField)skills.Children[1];
            Assert.AreEqual("levels", levels.FieldName);
            Assert.AreEqual(ArrayMode.Vertical, levels.Mode);
            Assert.AreEqual("E", levels.IndexColumn);
            Assert.AreEqual(2, levels.Children.Count);
        }

        [Test]
        public void HorizontalArray_StructureIsCorrect()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableArrayField("tags", null, ArrayMode.Horizontal, null, ",", new ParseableNode[]
                    {
                        new ParseableCustomField("tag", "B", FieldValueType.String),
                    }),
                }))
                .Build();

            var root = (ParseableDictionaryRoot)graph.Root;
            var tags = (ParseableArrayField)root.Children[0];
            Assert.AreEqual(ArrayMode.Horizontal, tags.Mode);
            Assert.AreEqual(",", tags.Separator);
            Assert.IsNull(tags.IndexColumn);
        }

        [Test]
        public void DictionaryField_StructureIsCorrect()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableDictionaryField("skillLevels", "SkillLevel", "C", KeyType.String, new ParseableNode[]
                    {
                        new ParseableCustomField("power", "D", FieldValueType.Int),
                    }),
                }))
                .Build();

            var root = (ParseableDictionaryRoot)graph.Root;
            var dictField = (ParseableDictionaryField)root.Children[0];
            Assert.AreEqual("skillLevels", dictField.FieldName);
            Assert.AreEqual("SkillLevel", dictField.TypeName);
            Assert.AreEqual("C", dictField.KeyColumn);
            Assert.AreEqual(KeyType.String, dictField.KeyType);
        }

        [Test]
        public void AssetField_StructureIsCorrect()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableAssetField("icon", "B", "Sprite", AssetLoadMethod.Addressables),
                }))
                .Build();

            var root = (ParseableDictionaryRoot)graph.Root;
            var asset = (ParseableAssetField)root.Children[0];
            Assert.AreEqual("icon", asset.FieldName);
            Assert.AreEqual("B", asset.Column);
            Assert.AreEqual("Sprite", asset.AssetTypeName);
            Assert.AreEqual(AssetLoadMethod.Addressables, asset.LoadMethod);
            Assert.IsTrue(asset.IsLeaf);
        }

        [Test]
        public void LeafNodes_HaveNoChildren()
        {
            var field = new ParseableCustomField("name", "A", FieldValueType.String);

            Assert.IsTrue(field.IsLeaf);
            Assert.AreEqual(0, field.Children.Count);
        }

        [Test]
        public void StructuralNodes_AreNotLeaf()
        {
            var obj = new ParseableObjectField("stats", "Stats", new ParseableNode[]
            {
                new ParseableCustomField("hp", "A", FieldValueType.Int),
            });

            Assert.IsFalse(obj.IsLeaf);
            Assert.AreEqual(1, obj.Children.Count);
        }

        [Test]
        public void CustomField_EnumType_IsPreserved()
        {
            var field = new ParseableCustomField(
                "rarity", "E", FieldValueType.Enum,
                enumType: typeof(DayOfWeek));

            Assert.AreEqual(FieldValueType.Enum, field.ValueType);
            Assert.AreEqual(typeof(DayOfWeek), field.EnumType);
        }

        [Test]
        public void CustomField_VectorSeparator_IsPreserved()
        {
            var field = new ParseableCustomField(
                "position", "C", FieldValueType.Vector2,
                separator: ";");

            Assert.AreEqual(";", field.Separator);
        }
    }
}
