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
