using System.Collections.Generic;
using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class ParsedDataTreeTests
    {
        [Test]
        public void ParsedValue_StoresTypedValue()
        {
            var val = new ParsedValue("damage", 42, typeof(int));

            Assert.AreEqual("damage", val.FieldName);
            Assert.AreEqual(42, val.Value);
            Assert.AreEqual(typeof(int), val.ValueType);
        }

        [Test]
        public void ParsedValue_StringType()
        {
            var val = new ParsedValue("name", "Sword", typeof(string));

            Assert.AreEqual("Sword", val.Value);
            Assert.AreEqual(typeof(string), val.ValueType);
        }

        [Test]
        public void ParsedObject_ContainsChildren()
        {
            var obj = new ParsedObject("stats", "ItemStats", new ParsedNode[]
            {
                new ParsedValue("hp", 100, typeof(int)),
                new ParsedValue("atk", 25.5f, typeof(float)),
            });

            Assert.AreEqual("stats", obj.FieldName);
            Assert.AreEqual("ItemStats", obj.TypeName);
            Assert.AreEqual(2, obj.Children.Count);

            var hp = (ParsedValue)obj.Children[0];
            Assert.AreEqual(100, hp.Value);
        }

        [Test]
        public void ParsedArray_ContainsElements()
        {
            var arr = new ParsedArray("rewards", "Reward", new ParsedNode[]
            {
                new ParsedObject(null, "Reward", new ParsedNode[]
                {
                    new ParsedValue("itemId", 10, typeof(int)),
                    new ParsedValue("quantity", 3, typeof(int)),
                }),
                new ParsedObject(null, "Reward", new ParsedNode[]
                {
                    new ParsedValue("itemId", 20, typeof(int)),
                    new ParsedValue("quantity", 1, typeof(int)),
                }),
            });

            Assert.AreEqual("rewards", arr.FieldName);
            Assert.AreEqual("Reward", arr.ElementTypeName);
            Assert.AreEqual(2, arr.Elements.Count);
        }

        [Test]
        public void ParsedDictionary_ContainsEntries()
        {
            var dict = new ParsedDictionary("skills", "int", "Skill", new Dictionary<object, ParsedNode>
            {
                { 10, new ParsedObject(null, "Skill", new ParsedNode[]
                    {
                        new ParsedValue("power", 100, typeof(int)),
                    })
                },
                { 20, new ParsedObject(null, "Skill", new ParsedNode[]
                    {
                        new ParsedValue("power", 200, typeof(int)),
                    })
                },
            });

            Assert.AreEqual("skills", dict.FieldName);
            Assert.AreEqual("int", dict.KeyTypeName);
            Assert.AreEqual("Skill", dict.ValueTypeName);
            Assert.AreEqual(2, dict.Entries.Count);
            Assert.IsTrue(dict.Entries.ContainsKey(10));
        }

        [Test]
        public void ParsedDataTree_HoldsRootAndWarnings()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                }))
                .Build();

            var warnings = new List<ValidationEntry>
            {
                new(ValidationSeverity.Warning, "empty cell at C3",
                    sourceCell: new CellReference(3, "C")),
            };

            var root = new ParsedDictionary(null, "int", "Item", new Dictionary<object, ParsedNode>
            {
                { 1, new ParsedObject(null, "Item", new ParsedNode[]
                    {
                        new ParsedValue("name", "Sword", typeof(string)),
                    })
                },
            });

            var tree = new ParsedDataTree(root, graph, warnings);

            Assert.AreSame(root, tree.Root);
            Assert.AreSame(graph, tree.SourceGraph);
            Assert.AreEqual(1, tree.ParseWarnings.Count);
            Assert.AreEqual(ValidationSeverity.Warning, tree.ParseWarnings[0].Severity);
        }

        [Test]
        public void ParsedDataTree_NullWarnings_DefaultsToEmpty()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Test")
                .WithRoot(new ParseableObjectRoot("Config", new ParseableNode[]
                {
                    new ParseableCustomField("fps", "A", FieldValueType.Int),
                }))
                .Build();

            var root = new ParsedObject(null, "Config", new ParsedNode[]
            {
                new ParsedValue("fps", 60, typeof(int)),
            });

            var tree = new ParsedDataTree(root, graph, null);

            Assert.AreEqual(0, tree.ParseWarnings.Count);
        }

        [Test]
        public void NestedStructure_ItemSkillsLevels()
        {
            var item = new ParsedObject(null, "Item", new ParsedNode[]
            {
                new ParsedValue("name", "Sword", typeof(string)),
                new ParsedArray("skills", "Skill", new ParsedNode[]
                {
                    new ParsedObject(null, "Skill", new ParsedNode[]
                    {
                        new ParsedValue("skillId", 10, typeof(int)),
                        new ParsedArray("levels", "Level", new ParsedNode[]
                        {
                            new ParsedObject(null, "Level", new ParsedNode[]
                            {
                                new ParsedValue("req", 1, typeof(int)),
                                new ParsedValue("power", 100, typeof(int)),
                            }),
                            new ParsedObject(null, "Level", new ParsedNode[]
                            {
                                new ParsedValue("req", 5, typeof(int)),
                                new ParsedValue("power", 200, typeof(int)),
                            }),
                        }),
                    }),
                }),
            });

            var skills = (ParsedArray)item.Children[1];
            Assert.AreEqual(1, skills.Elements.Count);

            var skill = (ParsedObject)skills.Elements[0];
            var levels = (ParsedArray)skill.Children[1];
            Assert.AreEqual(2, levels.Elements.Count);

            var level2 = (ParsedObject)levels.Elements[1];
            var power = (ParsedValue)level2.Children[1];
            Assert.AreEqual(200, power.Value);
        }
    }
}
