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
            _gen = new CodeGenerator("SO");
        }

        [Test]
        public void DictionaryRoot_GeneratesScriptableObject()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
                new ParseableCustomField("damage", "C", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public class ItemSO : ScriptableObject"));
            Assert.IsTrue(code.Contains("public string name;"));
            Assert.IsTrue(code.Contains("public int damage;"));
        }

        [Test]
        public void ArrayRoot_GeneratesScriptableObject()
        {
            var graph = MakeGraph(new ParseableArrayRoot("Level", new ParseableNode[]
            {
                new ParseableCustomField("difficulty", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class LevelSO : ScriptableObject"));
        }

        [Test]
        public void ObjectRoot_GeneratesScriptableObject()
        {
            var graph = MakeGraph(new ParseableObjectRoot("GameConfig", new ParseableNode[]
            {
                new ParseableCustomField("maxPlayers", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class GameConfigSO : ScriptableObject"));
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

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public ItemStatsSO stats;"));
            Assert.IsTrue(code.Contains("[Serializable]"));
            Assert.IsTrue(code.Contains("public class ItemStatsSO"));
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

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public List<RewardSO> rewards;"));
            Assert.IsTrue(code.Contains("[Serializable]"));
            Assert.IsTrue(code.Contains("public class RewardSO"));
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

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public List<string> tags;"));
            Assert.IsFalse(result.Value.Contains("[Serializable]"));
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

            var result = _gen.Generate(graph);

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

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public Dictionary<int, EffectSO> effects;"));
            Assert.IsTrue(code.Contains("public class EffectSO"));
        }

        [Test]
        public void AssetField_GeneratesTypedField()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableAssetField("icon", "B", "Sprite", AssetLoadMethod.Addressables),
            }));

            var result = _gen.Generate(graph);

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

            var result = _gen.Generate(graph);

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
        public void GeneratedCode_ContainsAutoGeneratedHeader()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Test", new ParseableNode[]
            {
                new ParseableCustomField("x", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("<auto-generated>"));
            Assert.IsTrue(result.Value.Contains("using UnityEngine;"));
        }

        [Test]
        public void NullGraph_ReturnsFailure()
        {
            var result = _gen.Generate(null);

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
