using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.CodeGen;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class QuantumCodeGeneratorTests
    {
        private QuantumCodeGenerator _gen;

        [SetUp]
        public void SetUp()
        {
            _gen = new QuantumCodeGenerator();
        }

        [Test]
        public void EntryClass_HasQuantumEntrySuffix()
        {
            var graph = MakeGraph(new ParseableObjectRoot("GameConfig", new ParseableNode[]
            {
                new ParseableCustomField("fps", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public class GameConfigQuantumEntry"));
        }

        [Test]
        public void IntField_GeneratesIntInSimSection()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("count", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public int count;"));
        }

        [Test]
        public void FloatField_GeneratesFP()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("speed", "A", FieldValueType.Float),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public FP speed;"));
        }

        [Test]
        public void DoubleField_GeneratesFP()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("ratio", "A", FieldValueType.Double),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public FP ratio;"));
        }

        [Test]
        public void BoolField_GeneratesBool()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("active", "A", FieldValueType.Bool),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public bool active;"));
        }

        [Test]
        public void Vector2Field_GeneratesFPVector2()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("pos", "A", FieldValueType.Vector2),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public FPVector2 pos;"));
        }

        [Test]
        public void Vector3Field_GeneratesFPVector3()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("dir", "A", FieldValueType.Vector3),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public FPVector3 dir;"));
        }

        [Test]
        public void StringField_GoesIntoQuantumUnityBlock()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("name", "A", FieldValueType.String),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int nameIndex = code.IndexOf("public string name;");
            Assert.IsTrue(ifIndex >= 0, "Expected #if QUANTUM_UNITY block");
            Assert.IsTrue(nameIndex > ifIndex, "String field should appear after #if QUANTUM_UNITY");
        }

        [Test]
        public void ColorField_GoesIntoQuantumUnityBlock()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("tint", "A", FieldValueType.Color),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int tintIndex = code.IndexOf("tint;");
            Assert.IsTrue(ifIndex >= 0, "Expected #if QUANTUM_UNITY block");
            Assert.IsTrue(tintIndex > ifIndex, "Color field should appear after #if QUANTUM_UNITY");
        }

        [Test]
        public void AssetField_GoesIntoQuantumUnityBlock()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableAssetField("icon", "A", AssetType.Sprite),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int iconIndex = code.IndexOf("icon;");
            Assert.IsTrue(ifIndex >= 0, "Expected #if QUANTUM_UNITY block");
            Assert.IsTrue(iconIndex > ifIndex, "Asset field should appear after #if QUANTUM_UNITY");
        }

        [Test]
        public void StringArray_GoesIntoQuantumUnityBlock()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableArrayField("tags", null, ArrayMode.Horizontal, null, ",", new ParseableNode[]
                {
                    new ParseableCustomField("tag", "B", FieldValueType.String),
                }),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int tagsIndex = code.IndexOf("List<string> tags");
            Assert.IsTrue(ifIndex >= 0, "Expected #if QUANTUM_UNITY block");
            Assert.IsTrue(tagsIndex > ifIndex, "String array should appear after #if QUANTUM_UNITY");
            Assert.IsFalse(code.Contains("List<int> tags"), "String array must not generate List<int>");
        }

        [Test]
        public void IntArray_StaysInSimSection()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("name", "A", FieldValueType.String),
                new ParseableArrayField("scores", null, ArrayMode.Horizontal, null, ",", new ParseableNode[]
                {
                    new ParseableCustomField("score", "B", FieldValueType.Int),
                }),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int scoresIndex = code.IndexOf("List<int> scores");
            Assert.IsTrue(ifIndex >= 0, "Expected #if QUANTUM_UNITY block due to string field");
            Assert.IsTrue(scoresIndex >= 0, "Expected List<int> scores");
            Assert.IsTrue(scoresIndex < ifIndex, "Int array should appear before #if QUANTUM_UNITY");
        }

        [Test]
        public void NestedObject_GeneratesQuantumEntrySuffix()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableObjectField("stats", "ItemStats", new ParseableNode[]
                {
                    new ParseableCustomField("hp", "B", FieldValueType.Int),
                }),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public ItemStatsQuantumEntry stats;"));
            Assert.IsTrue(code.Contains("public class ItemStatsQuantumEntry"));
        }

        [Test]
        public void DictionaryField_GeneratesParallelKeyValueLists()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableDictionaryField("bonuses", null, "A", KeyType.String, new ParseableNode[]
                {
                    new ParseableCustomField("val", "B", FieldValueType.Int),
                }),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("bonusesKeys"));
            Assert.IsTrue(code.Contains("bonusesValues"));
        }

        [Test]
        public void DictionaryRoot_IntKey_GeneratesGetById()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public class TestQuantumDatabase"));
            Assert.IsTrue(code.Contains("GetById(int key)"));
        }

        [Test]
        public void DictionaryRoot_StringKey_GeneratesStringLookup()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Region", "A", KeyType.String, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("_stringLookup"));
            Assert.IsTrue(code.Contains("GetById(string key)"));
        }

        [Test]
        public void ArrayRoot_GeneratesGetByIndex()
        {
            var graph = MakeGraph(new ParseableArrayRoot("Level", new ParseableNode[]
            {
                new ParseableCustomField("difficulty", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("GetByIndex(int index)"));
        }

        [Test]
        public void ObjectRoot_GeneratesGetObject()
        {
            var graph = MakeGraph(new ParseableObjectRoot("GameConfig", new ParseableNode[]
            {
                new ParseableCustomField("maxPlayers", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("GetObject()"));
        }

        [Test]
        public void EnumField_InSimSection()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableEnumField("rarity", "A", "Rarity"),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public Rarity rarity;"));
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int rarityIndex = code.IndexOf("public Rarity rarity;");
            Assert.IsTrue(ifIndex < 0 || rarityIndex < ifIndex, "Enum field should be in sim section");
        }

        [Test]
        public void FlagField_InSimSection()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Character", new ParseableNode[]
            {
                new ParseableFlagField("status", "A", "StatusFlag", "|"),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public StatusFlag status;"));
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int statusIndex = code.IndexOf("public StatusFlag status;");
            Assert.IsTrue(ifIndex < 0 || statusIndex < ifIndex, "Flag field should be in sim section");
        }

        [Test]
        public void MixedFields_SimFieldsBeforeViewFields()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("damage", "A", FieldValueType.Int),
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            int damageIndex = code.IndexOf("public int damage;");
            int ifIndex = code.IndexOf("#if QUANTUM_UNITY");
            int nameIndex = code.IndexOf("public string name;");
            Assert.IsTrue(damageIndex < ifIndex, "Sim field should appear before #if QUANTUM_UNITY");
            Assert.IsTrue(nameIndex > ifIndex, "View field should appear after #if QUANTUM_UNITY");
        }

        [Test]
        public void GeneratedCode_ContainsQuantumUsings()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("x", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("using Photon.Deterministic;"));
            Assert.IsTrue(result.Value.Contains("using Quantum;"));
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
