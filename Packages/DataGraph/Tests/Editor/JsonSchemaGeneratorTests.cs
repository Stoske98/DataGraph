using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Serialization;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class JsonSchemaGeneratorTests
    {
        private JsonSchemaGenerator _gen;

        [SetUp]
        public void SetUp()
        {
            _gen = new JsonSchemaGenerator();
        }

        [Test]
        public void DoubleField_GeneratesNumberType()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("ratio", "A", FieldValueType.Double),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.That(result.Value, Does.Contain("\"ratio\": {\"type\": \"number\"}"));
        }

        [Test]
        public void Vector2Field_GeneratesNestedXYSchema()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("pos", "A", FieldValueType.Vector2),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var schema = result.Value;
            Assert.That(schema, Does.Contain("\"pos\":"));
            Assert.That(schema, Does.Contain("\"type\": \"object\""));
            Assert.That(schema, Does.Contain("\"x\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"y\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"required\": [\"x\", \"y\"]"));
        }

        [Test]
        public void Vector3Field_GeneratesNestedXYZSchema()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("dir", "A", FieldValueType.Vector3),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var schema = result.Value;
            Assert.That(schema, Does.Contain("\"dir\":"));
            Assert.That(schema, Does.Contain("\"type\": \"object\""));
            Assert.That(schema, Does.Contain("\"x\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"y\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"z\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"required\": [\"x\", \"y\", \"z\"]"));
        }

        [Test]
        public void ColorField_GeneratesRGBASchema()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("tint", "A", FieldValueType.Color),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            var schema = result.Value;
            Assert.That(schema, Does.Contain("\"tint\":"));
            Assert.That(schema, Does.Contain("\"type\": \"object\""));
            Assert.That(schema, Does.Contain("\"r\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"g\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"b\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"a\": {\"type\": \"number\"}"));
            Assert.That(schema, Does.Contain("\"required\": [\"r\", \"g\", \"b\", \"a\"]"));
        }

        [Test]
        public void IntField_GeneratesIntegerType()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("damage", "A", FieldValueType.Int),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.That(result.Value, Does.Contain("\"damage\": {\"type\": \"integer\"}"));
        }

        [Test]
        public void StringField_GeneratesStringType()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("name", "A", FieldValueType.String),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.That(result.Value, Does.Contain("\"name\": {\"type\": \"string\"}"));
        }

        [Test]
        public void EnumField_GeneratesStringType()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("rarity", "A", FieldValueType.Enum),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.That(result.Value, Does.Contain("\"rarity\": {\"type\": \"string\"}"));
        }

        [Test]
        public void BoolField_GeneratesBooleanType()
        {
            var graph = MakeGraph(new ParseableObjectRoot("Item", new ParseableNode[]
            {
                new ParseableCustomField("enabled", "A", FieldValueType.Bool),
            }));

            var result = _gen.Generate(graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.That(result.Value, Does.Contain("\"enabled\": {\"type\": \"boolean\"}"));
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
