using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.CodeGen;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class CodeGeneratorTests
    {
        private CodeGenerator _gen;

        [SetUp]
        public void SetUp()
        {
            _gen = new CodeGenerator();
        }

        [Test]
        public void DictionaryRoot_GeneratesEntryWithDataGraphEntry()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
                new ParseableCustomField("damage", "C", FieldValueType.Int),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public class Item : DataGraphEntry"));
            Assert.IsTrue(code.Contains("public string name;"));
            Assert.IsTrue(code.Contains("public int damage;"));
        }

        [Test]
        public void DictionaryRoot_GeneratesDatabaseClass()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.GenerateDatabase(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class TestDatabase : DictionaryDatabaseAsset<int, Item>"));
        }

        [Test]
        public void ArrayRoot_GeneratesEntryWithDataGraphEntry()
        {
            var graph = MakeGraph(new ParseableArrayRoot("Level", new ParseableNode[]
            {
                new ParseableCustomField("difficulty", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class Level : DataGraphEntry"));
        }

        [Test]
        public void ArrayRoot_GeneratesDatabaseClass()
        {
            var graph = MakeGraph(new ParseableArrayRoot("Level", new ParseableNode[]
            {
                new ParseableCustomField("difficulty", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateDatabase(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class TestDatabase : ArrayDatabaseAsset<Level>"));
        }

        [Test]
        public void ObjectRoot_GeneratesEntryWithDataGraphEntry()
        {
            var graph = MakeGraph(new ParseableObjectRoot("GameConfig", new ParseableNode[]
            {
                new ParseableCustomField("maxPlayers", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class GameConfig : DataGraphEntry"));
        }

        [Test]
        public void ObjectRoot_GeneratesDatabaseClass()
        {
            var graph = MakeGraph(new ParseableObjectRoot("GameConfig", new ParseableNode[]
            {
                new ParseableCustomField("maxPlayers", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateDatabase(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class TestDatabase : ObjectDatabaseAsset<GameConfig>"));
        }

        [Test]
        public void NestedObjectField_GeneratesSerializableClass()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableObjectField("stats", "ItemStats", new ParseableNode[]
                {
                    new ParseableCustomField("hp", "B", FieldValueType.Int),
                    new ParseableCustomField("speed", "C", FieldValueType.Float),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public ItemStats stats;"));
            Assert.IsTrue(code.Contains("[Serializable]"));
            Assert.IsTrue(code.Contains("public class ItemStats"));
            Assert.IsTrue(code.Contains("public int hp;"));
            Assert.IsTrue(code.Contains("public float speed;"));
        }

        [Test]
        public void VerticalArray_StructuralChildren_GeneratesListAndNestedClass()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableArrayField("rewards", "Reward", ArrayMode.Vertical, "B", null, new ParseableNode[]
                {
                    new ParseableCustomField("itemId", "C", FieldValueType.Int),
                    new ParseableCustomField("quantity", "D", FieldValueType.Int),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public List<Reward> rewards;"));
            Assert.IsTrue(code.Contains("[Serializable]"));
            Assert.IsTrue(code.Contains("public class Reward"));
            Assert.IsTrue(code.Contains("public int itemId;"));
        }

        [Test]
        public void HorizontalArray_PrimitiveChildren_GeneratesListOfPrimitive()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableArrayField("tags", null, ArrayMode.Horizontal, null, ",", new ParseableNode[]
                {
                    new ParseableCustomField("tag", "B", FieldValueType.String),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public List<string> tags;"));
            Assert.IsTrue(code.Contains("public class Item : DataGraphEntry"));
            Assert.IsFalse(code.Contains("public class Tag"));
        }

        [Test]
        public void DictionaryField_GeneratesDictionaryProperty()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableDictionaryField("props", "Prop", "C", KeyType.String, new ParseableNode[]
                {
                    new ParseableCustomField("value", "D", FieldValueType.String),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public Dictionary<string, string> props;"));
        }

        [Test]
        public void DictionaryField_StructuralValue_GeneratesNestedClass()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableDictionaryField("effects", "Effect", "C", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "D", FieldValueType.String),
                    new ParseableCustomField("power", "E", FieldValueType.Int),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public Dictionary<int, Effect> effects;"));
            Assert.IsTrue(code.Contains("public class Effect"));
        }

        [Test]
        public void AssetField_Addressables_GeneratesStringField()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableAssetField("icon", "B", AssetType.Sprite, AssetLoadMethod.Addressables),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public string icon;"));
        }

        [Test]
        public void AssetField_AssetDatabase_GeneratesTypedField()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableAssetField("icon", "B", AssetType.Sprite, AssetLoadMethod.AssetDatabase),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public Sprite icon;"));
        }

        [Test]
        public void AllPrimitiveTypes_GenerateCorrectFields()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("a", "A", FieldValueType.String),
                new ParseableCustomField("b", "B", FieldValueType.Int),
                new ParseableCustomField("c", "C", FieldValueType.Float),
                new ParseableCustomField("d", "D", FieldValueType.Bool),
                new ParseableCustomField("e", "E", FieldValueType.Vector2),
                new ParseableCustomField("f", "F", FieldValueType.Vector3),
                new ParseableCustomField("g", "G", FieldValueType.Color),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public string a;"));
            Assert.IsTrue(code.Contains("public int b;"));
            Assert.IsTrue(code.Contains("public float c;"));
            Assert.IsTrue(code.Contains("public bool d;"));
            Assert.IsTrue(code.Contains("public Vector2 e;"));
            Assert.IsTrue(code.Contains("public Vector3 f;"));
            Assert.IsTrue(code.Contains("public Color g;"));
        }

        [Test]
        public void GeneratedCode_ContainsHeaderAndNamespace()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Test", new ParseableNode[]
            {
                new ParseableCustomField("x", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("<auto-generated>"));
            Assert.IsTrue(result.Value.Contains("using UnityEngine;"));
            Assert.IsTrue(result.Value.Contains("namespace DataGraph.Data"));
        }

        [Test]
        public void NullGraph_ReturnsFailure()
        {
            var result = _gen.GenerateEntries(null);

            Assert.IsTrue(result.IsFailure);
        }

        private static ParseableGraph MakeGraph(ParseableNode root)
        {
            return new ParseableGraphBuilder()
                .WithGraphName("Test")
                .WithRoot(root)
                .Build();
        }
    }
}
