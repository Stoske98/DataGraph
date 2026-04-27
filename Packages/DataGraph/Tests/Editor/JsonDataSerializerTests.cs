using System.Collections.Generic;
using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Serialization;
using DataGraph.Runtime;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class JsonDataSerializerTests
    {
        [Test]
        public void DictionaryRoot_SerializesAsObject()
        {
            var tree = MakeTree(new ParsedDictionary(null, "int", "Item",
                new Dictionary<object, ParsedNode>
                {
                    { 1, new ParsedObject(null, "Item", new ParsedNode[]
                        {
                            new ParsedValue("name", "Sword", typeof(string)),
                            new ParsedValue("damage", 100, typeof(int)),
                        })
                    },
                }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            var json = result.Value;
            Assert.IsTrue(json.Contains("\"1\":{"));
            Assert.IsTrue(json.Contains("\"name\":\"Sword\""));
            Assert.IsTrue(json.Contains("\"damage\":100"));
        }

        [Test]
        public void ArrayRoot_SerializesAsArray()
        {
            var tree = MakeTree(new ParsedArray(null, "Level", new ParsedNode[]
            {
                new ParsedObject(null, "Level", new ParsedNode[]
                {
                    new ParsedValue("difficulty", 1, typeof(int)),
                }),
                new ParsedObject(null, "Level", new ParsedNode[]
                {
                    new ParsedValue("difficulty", 2, typeof(int)),
                }),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.StartsWith("["));
            Assert.IsTrue(result.Value.Contains("\"difficulty\":1"));
            Assert.IsTrue(result.Value.Contains("\"difficulty\":2"));
        }

        [Test]
        public void BoolValues_SerializeAsLowercase()
        {
            var tree = MakeTree(new ParsedObject(null, "Config", new ParsedNode[]
            {
                new ParsedValue("enabled", true, typeof(bool)),
                new ParsedValue("debug", false, typeof(bool)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"enabled\":true"));
            Assert.IsTrue(result.Value.Contains("\"debug\":false"));
        }

        [Test]
        public void FloatValues_SerializeWithInvariantCulture()
        {
            var tree = MakeTree(new ParsedObject(null, "Config", new ParsedNode[]
            {
                new ParsedValue("speed", 3.14f, typeof(float)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("3.14"));
            Assert.IsFalse(result.Value.Contains("3,14"));
        }

        [Test]
        public void NestedArray_SerializesCorrectly()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("name", "Sword", typeof(string)),
                new ParsedArray("tags", null, new ParsedNode[]
                {
                    new ParsedValue(null, "fire", typeof(string)),
                    new ParsedValue(null, "rare", typeof(string)),
                }),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"tags\":[\"fire\",\"rare\"]"));
        }

        [Test]
        public void NullValue_OmitMode_SkipsField()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("name", "Sword", typeof(string)),
                new ParsedValue("desc", null, typeof(string)),
            }));

            var serializer = new JsonDataSerializer(
                prettyPrint: false,
                nullHandling: JsonDataSerializer.NullHandling.Omit);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.Value.Contains("\"desc\""));
        }

        [Test]
        public void NullValue_IncludeMode_WritesNull()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("name", "Sword", typeof(string)),
                new ParsedValue("desc", null, typeof(string)),
            }));

            var serializer = new JsonDataSerializer(
                prettyPrint: false,
                nullHandling: JsonDataSerializer.NullHandling.IncludeAsNull);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"desc\":null"));
        }

        [Test]
        public void PrettyPrint_AddsIndentation()
        {
            var tree = MakeTree(new ParsedObject(null, "Config", new ParsedNode[]
            {
                new ParsedValue("fps", 60, typeof(int)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: true);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\n"));
            Assert.IsTrue(result.Value.Contains("  "));
        }

        [Test]
        public void StringEscaping_HandlesSpecialChars()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("desc", "He said \"hello\"\nNew line", typeof(string)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\\\"hello\\\""));
            Assert.IsTrue(result.Value.Contains("\\n"));
        }

        [Test]
        public void NullTree_ReturnsFailure()
        {
            var serializer = new JsonDataSerializer();
            var result = serializer.Serialize(null);

            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void DoubleValue_SerializesAsNumber()
        {
            var tree = MakeTree(new ParsedObject(null, "Config", new ParsedNode[]
            {
                new ParsedValue("ratio", 3.14, typeof(double)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"ratio\":3.14"));
            Assert.IsFalse(result.Value.Contains("\"ratio\":\"3.14\""));
        }

        [Test]
        public void Vector2Value_SerializesAsJsonObject()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("pos", new UnityEngine.Vector2(1f, 2f), typeof(UnityEngine.Vector2)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"pos\":{\"x\":1,\"y\":2}"));
        }

        [Test]
        public void Vector3Value_SerializesAsJsonObject()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("dir", new UnityEngine.Vector3(0f, 1f, 0f), typeof(UnityEngine.Vector3)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"dir\":{\"x\":0,\"y\":1,\"z\":0}"));
        }

        [Test]
        public void ColorValue_SerializesAsJsonObject()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("tint", new UnityEngine.Color(1f, 0f, 0f, 1f), typeof(UnityEngine.Color)),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"tint\":{\"r\":1,\"g\":0,\"b\":0,\"a\":1}"));
        }

        [Test]
        public void NestedDictionary_SerializesAsNestedObject()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedDictionary("bonuses", "string", "int",
                    new Dictionary<object, ParsedNode>
                    {
                        { "fire", new ParsedValue(null, 10, typeof(int)) },
                    }),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"bonuses\":{\"fire\":10}"));
        }

        [Test]
        public void AssetReference_SerializesAsPathString()
        {
            var tree = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedAssetReference("icon", "Assets/Art/sword.png", AssetType.Sprite),
            }));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.Contains("\"icon\":\"Assets/Art/sword.png\""));
        }

        [Test]
        public void EmptyDictionary_SerializesAsEmptyObject()
        {
            var tree = MakeTree(new ParsedDictionary(null, "string", "int",
                new Dictionary<object, ParsedNode>()));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("{}", result.Value);
        }

        [Test]
        public void EmptyArray_SerializesAsEmptyArray()
        {
            var tree = MakeTree(new ParsedArray(null, "Item", new ParsedNode[0]));

            var serializer = new JsonDataSerializer(prettyPrint: false);
            var result = serializer.Serialize(tree);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("[]", result.Value);
        }

        [Test]
        public void SerializeTwice_DifferentTrees_NoStateLeakage()
        {
            var serializer = new JsonDataSerializer(prettyPrint: true);

            var tree1 = MakeTree(new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("name", "Sword", typeof(string)),
            }));

            var tree2 = MakeTree(new ParsedObject(null, "Config", new ParsedNode[]
            {
                new ParsedValue("level", 5, typeof(int)),
                new ParsedObject("nested", "Stats", new ParsedNode[]
                {
                    new ParsedValue("hp", 100, typeof(int)),
                    new ParsedValue("mp", 50, typeof(int)),
                }),
            }));

            var result1 = serializer.Serialize(tree1);
            var result2 = serializer.Serialize(tree2);

            Assert.IsTrue(result1.IsSuccess);
            Assert.IsTrue(result2.IsSuccess);

            var fresh1 = new JsonDataSerializer(prettyPrint: true).Serialize(tree1);
            var fresh2 = new JsonDataSerializer(prettyPrint: true).Serialize(tree2);

            Assert.AreEqual(fresh1.Value, result1.Value,
                "First result must match a fresh-instance serialization of the same tree.");
            Assert.AreEqual(fresh2.Value, result2.Value,
                "Second result must match a fresh-instance serialization of the same tree.");
        }

        private static ParsedDataTree MakeTree(ParsedNode root)
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Test")
                .WithRoot(new ParseableObjectRoot("Dummy", new ParseableNode[]
                {
                    new ParseableCustomField("x", "A", FieldValueType.Int),
                }))
                .Build();

            return new ParsedDataTree(root, graph, null);
        }
    }
}
