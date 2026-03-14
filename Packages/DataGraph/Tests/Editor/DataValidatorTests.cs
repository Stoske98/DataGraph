using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Parsing;
using DataGraph.Runtime;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class DataValidatorTests
    {
        private DataValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _validator = new DataValidator();
        }

        [Test]
        public void ValidTree_ReturnsValidReport()
        {
            var tree = MakeTree(new ParsedDictionary(null, "int", "Item",
                new Dictionary<object, ParsedNode>
                {
                    { 1, new ParsedObject(null, "Item", new ParsedNode[]
                        {
                            new ParsedValue("name", "Sword", typeof(string)),
                        })
                    },
                }));

            var report = _validator.Validate(tree);

            Assert.IsTrue(report.IsValid);
        }

        [Test]
        public void NullRoot_ReturnsError()
        {
            var tree = MakeTree(null);

            var report = _validator.Validate(tree);

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void EmptyDictionary_ReturnsWarning()
        {
            var tree = MakeTree(new ParsedDictionary(null, "int", "Item",
                new Dictionary<object, ParsedNode>()));

            var report = _validator.Validate(tree);

            Assert.IsTrue(report.IsValid);
            Assert.IsTrue(report.HasWarnings);
        }

        [Test]
        public void EmptyArray_ReturnsInfo()
        {
            var tree = MakeTree(new ParsedArray(null, "Level",
                new List<ParsedNode>()));

            var report = _validator.Validate(tree);

            Assert.IsTrue(report.IsValid);
            Assert.IsTrue(report.Entries.Any(e => e.Severity == ValidationSeverity.Info));
        }

        [Test]
        public void ParseWarnings_IncludedInReport()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Test")
                .WithRoot(new ParseableObjectRoot("Cfg", new ParseableNode[]
                {
                    new ParseableCustomField("x", "A", FieldValueType.Int),
                }))
                .Build();

            var warnings = new List<ValidationEntry>
            {
                new(ValidationSeverity.Warning, "coercion fallback at C2"),
            };

            var tree = new ParsedDataTree(
                new ParsedObject(null, "Cfg", new ParsedNode[]
                {
                    new ParsedValue("x", 0, typeof(int)),
                }),
                graph,
                warnings);

            var report = _validator.Validate(tree);

            Assert.IsTrue(report.Entries.Any(e => e.Message.Contains("coercion fallback")));
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
