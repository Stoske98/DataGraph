using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.CodeGen;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class BlobCodeGeneratorTests
    {
        private BlobCodeGenerator _gen;

        [SetUp]
        public void SetUp()
        {
            _gen = new BlobCodeGenerator();
        }

        [Test]
        public void StringField_GeneratesBlobString()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("name", "A", FieldValueType.String),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public BlobString name;"));
        }

        [Test]
        public void IntField_GeneratesInt()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("count", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public int count;"));
        }

        [Test]
        public void FloatField_GeneratesFloat()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("speed", "A", FieldValueType.Float),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public float speed;"));
        }

        [Test]
        public void DoubleField_GeneratesDouble()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("ratio", "A", FieldValueType.Double),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public double ratio;"));
        }

        [Test]
        public void BoolField_GeneratesBool()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("active", "A", FieldValueType.Bool),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public bool active;"));
        }

        [Test]
        public void Vector2Field_GeneratesUnityVector2()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("pos", "A", FieldValueType.Vector2),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public UnityEngine.Vector2 pos;"));
        }

        [Test]
        public void Vector3Field_GeneratesUnityVector3()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("dir", "A", FieldValueType.Vector3),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public UnityEngine.Vector3 dir;"));
        }

        [Test]
        public void ColorField_GeneratesUnityColor()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Config", new ParseableNode[]
            {
                new ParseableCustomField("tint", "A", FieldValueType.Color),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public UnityEngine.Color tint;"));
        }

        [Test]
        public void IntArray_GeneratesBlobArrayInt()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableArrayField("scores", null, ArrayMode.Horizontal, null, ",", new ParseableNode[]
                {
                    new ParseableCustomField("score", "A", FieldValueType.Int),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public BlobArray<int> scores;"));
        }

        [Test]
        public void StringArray_GeneratesBlobArrayBlobString()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableArrayField("tags", null, ArrayMode.Horizontal, null, ",", new ParseableNode[]
                {
                    new ParseableCustomField("tag", "A", FieldValueType.String),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public BlobArray<BlobString> tags;"));
        }

        [Test]
        public void DictionaryField_IntKey_GeneratesBlobArrayIntKeysAndValues()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableDictionaryField("bonuses", null, "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("val", "B", FieldValueType.Float),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public BlobArray<int> bonusesKeys;"));
            Assert.IsTrue(code.Contains("public BlobArray<float> bonusesValues;"));
        }

        [Test]
        public void DictionaryField_StringKey_GeneratesBlobStringKeys()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableDictionaryField("stats", null, "A", KeyType.String, new ParseableNode[]
                {
                    new ParseableCustomField("val", "B", FieldValueType.Int),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public BlobArray<BlobString> statsKeys;"));
            Assert.IsTrue(code.Contains("public BlobArray<int> statsValues;"));
        }

        [Test]
        public void NestedObject_GeneratesNestedBlobStruct()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableObjectField("stats", "ItemStats", new ParseableNode[]
                {
                    new ParseableCustomField("hp", "A", FieldValueType.Int),
                }),
            }));

            var result = _gen.GenerateEntries(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public ItemStatsBlob stats;"));
            Assert.IsTrue(code.Contains("public struct ItemStatsBlob"));
        }

        [Test]
        public void DictionaryRoot_IntKey_GeneratesDatabaseWithBinarySearch()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.GenerateDatabase(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public struct TestBlobDatabase"));
            Assert.IsTrue(code.Contains("GetById(int key)"));
            Assert.IsTrue(code.Contains("lo = 0"));
            Assert.IsTrue(code.Contains("hi ="));
        }

        [Test]
        public void DictionaryRoot_StringKey_GeneratesGetByIdWithStringCompare()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Region", "A", KeyType.String, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.GenerateDatabase(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("GetById(string key)"));
            Assert.IsTrue(code.Contains("string.Compare"));
        }

        [Test]
        public void ArrayRoot_GeneratesGetByIndex()
        {
            var graph = MakeGraph(new ParseableArrayRoot("Level", new ParseableNode[]
            {
                new ParseableCustomField("difficulty", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateDatabase(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("GetByIndex(int index)"));
        }

        [Test]
        public void ObjectRoot_GeneratesPublicDataField()
        {
            var graph = MakeGraph(new ParseableObjectRoot("GameConfig", new ParseableNode[]
            {
                new ParseableCustomField("maxPlayers", "A", FieldValueType.Int),
            }));

            var result = _gen.GenerateDatabase(graph);

            Assert.IsTrue(result.IsSuccess);
            // Object Blob databases expose a public 'data' field rather than
            // a GetObject() instance method. Returning ref to a struct field
            // from an instance member is illegal (CS8170), so callers access
            // the data field directly via dbRef.Value.data.
            Assert.IsTrue(result.Value.Contains("public GameConfigBlob data;"));
            Assert.IsFalse(result.Value.Contains("GetObject()"),
                "Object Blob database must not declare a GetObject() instance method.");
        }

        [Test]
        public void Builder_GeneratesBuildMethod()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.GenerateBuilder(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public static BlobAssetReference<TestBlobDatabase> Build("));
        }

        [Test]
        public void Builder_GeneratesSaveAndLoadMethods()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.GenerateBuilder(graph);

            Assert.IsTrue(result.IsSuccess);
            var code = result.Value;
            Assert.IsTrue(code.Contains("public static void Save("));
            Assert.IsTrue(code.Contains("public static BlobAssetReference<TestBlobDatabase> Load("));
        }

        [Test]
        public void Builder_GeneratesSourceStruct()
        {
            var graph = MakeGraph(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
            {
                new ParseableCustomField("name", "B", FieldValueType.String),
            }));

            var result = _gen.GenerateBuilder(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("public struct ItemBlobSource"));
        }

        [Test]
        public void NullGraph_ReturnsFailure()
        {
            Assert.IsTrue(_gen.GenerateEntries(null).IsFailure);
            Assert.IsTrue(_gen.GenerateDatabase(null).IsFailure);
            Assert.IsTrue(_gen.GenerateBuilder(null).IsFailure);
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
