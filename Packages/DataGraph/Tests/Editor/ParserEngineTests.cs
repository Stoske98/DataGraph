using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Parsing;
using DataGraph.Runtime;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class ParserEngineTests
    {
        private ParserEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _engine = new ParserEngine();
        }

        [Test]
        public void DictionaryRoot_FlatFields_ParsesCorrectly()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                    new ParseableCustomField("damage", "C", FieldValueType.Int),
                }))
                .Build();

            var data = MakeTable(
                new[] { "ID", "Name", "Damage" },
                new[] { "1", "Sword", "100" },
                new[] { "2", "Shield", "25" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            Assert.AreEqual(2, dict.Entries.Count);

            var sword = (ParsedObject)dict.Entries[1];
            Assert.AreEqual("Item", sword.TypeName);
            Assert.AreEqual("Sword", ((ParsedValue)sword.Children[0]).Value);
            Assert.AreEqual(100, ((ParsedValue)sword.Children[1]).Value);
        }

        [Test]
        public void DictionaryRoot_StringKey_ParsesCorrectly()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Localization")
                .WithRoot(new ParseableDictionaryRoot("Locale", "A", KeyType.String, new ParseableNode[]
                {
                    new ParseableCustomField("text", "B", FieldValueType.String),
                }))
                .Build();

            var data = MakeTable(
                new[] { "Key", "Text" },
                new[] { "menu.start", "Start Game" },
                new[] { "menu.quit", "Quit" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            Assert.AreEqual(2, dict.Entries.Count);
            Assert.IsTrue(dict.Entries.ContainsKey("menu.start"));
        }

        [Test]
        public void ArrayRoot_ParsesAllRows()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Levels")
                .WithRoot(new ParseableArrayRoot("Level", new ParseableNode[]
                {
                    new ParseableCustomField("difficulty", "A", FieldValueType.Int),
                    new ParseableCustomField("name", "B", FieldValueType.String),
                }))
                .Build();

            var data = MakeTable(
                new[] { "Difficulty", "Name" },
                new[] { "1", "Easy" },
                new[] { "2", "Medium" },
                new[] { "3", "Hard" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var arr = (ParsedArray)result.Value.Root;
            Assert.AreEqual(3, arr.Elements.Count);

            var first = (ParsedObject)arr.Elements[0];
            Assert.AreEqual(1, ((ParsedValue)first.Children[0]).Value);
            Assert.AreEqual("Easy", ((ParsedValue)first.Children[1]).Value);
        }

        [Test]
        public void ObjectRoot_ParsesSingleRow()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Config")
                .WithRoot(new ParseableObjectRoot("GameConfig", new ParseableNode[]
                {
                    new ParseableCustomField("maxPlayers", "A", FieldValueType.Int),
                    new ParseableCustomField("gameName", "B", FieldValueType.String),
                }))
                .Build();

            var data = MakeTable(
                new[] { "MaxPlayers", "GameName" },
                new[] { "4", "Battle Arena" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var obj = (ParsedObject)result.Value.Root;
            Assert.AreEqual("GameConfig", obj.TypeName);
            Assert.AreEqual(4, ((ParsedValue)obj.Children[0]).Value);
            Assert.AreEqual("Battle Arena", ((ParsedValue)obj.Children[1]).Value);
        }

        [Test]
        public void HorizontalArray_SplitsBySeparator()
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

            var data = MakeTable(
                new[] { "ID", "Tags" },
                new[] { "1", "fire,rare,weapon" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            var item = (ParsedObject)dict.Entries[1];
            var tags = (ParsedArray)item.Children[0];
            Assert.AreEqual(3, tags.Elements.Count);
            Assert.AreEqual("fire", ((ParsedValue)tags.Elements[0]).Value);
            Assert.AreEqual("rare", ((ParsedValue)tags.Elements[1]).Value);
            Assert.AreEqual("weapon", ((ParsedValue)tags.Elements[2]).Value);
        }

        [Test]
        public void HorizontalArray_IntElements()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableArrayField("slots", null, ArrayMode.Horizontal, null, ",", new ParseableNode[]
                    {
                        new ParseableCustomField("slot", "B", FieldValueType.Int),
                    }),
                }))
                .Build();

            var data = MakeTable(
                new[] { "ID", "Slots" },
                new[] { "1", "10,20,30" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            var item = (ParsedObject)dict.Entries[1];
            var slots = (ParsedArray)item.Children[0];
            Assert.AreEqual(3, slots.Elements.Count);
            Assert.AreEqual(10, ((ParsedValue)slots.Elements[0]).Value);
            Assert.AreEqual(30, ((ParsedValue)slots.Elements[2]).Value);
        }

        [Test]
        public void HorizontalArray_EmptyCell_ReturnsEmptyArray()
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

            var data = MakeTable(
                new[] { "ID", "Tags" },
                new[] { "1", "" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            var item = (ParsedObject)dict.Entries[1];
            var tags = (ParsedArray)item.Children[0];
            Assert.AreEqual(0, tags.Elements.Count);
        }

        [Test]
        public void VerticalArray_SimpleElements()
        {
            // Item with rewards spread across rows
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                    new ParseableArrayField("rewards", "Reward", ArrayMode.Vertical, "C", null, new ParseableNode[]
                    {
                        new ParseableCustomField("gold", "D", FieldValueType.Int),
                    }),
                }))
                .Build();

            // ID  Name   RwdIdx  Gold
            // 1   Sword  0       100
            //            1       200
            //            2       350
            // 2   Shield 0       50
            var data = MakeTable(
                new[] { "ID", "Name", "RwdIdx", "Gold" },
                new[] { "1", "Sword", "0", "100" },
                new[] { "", "", "1", "200" },
                new[] { "", "", "2", "350" },
                new[] { "2", "Shield", "0", "50" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            Assert.AreEqual(2, dict.Entries.Count);

            var sword = (ParsedObject)dict.Entries[1];
            Assert.AreEqual("Sword", ((ParsedValue)sword.Children[0]).Value);
            var rewards = (ParsedArray)sword.Children[1];
            Assert.AreEqual(3, rewards.Elements.Count);
            Assert.AreEqual(100, ((ParsedValue)rewards.Elements[0]).Value);
            Assert.AreEqual(200, ((ParsedValue)rewards.Elements[1]).Value);
            Assert.AreEqual(350, ((ParsedValue)rewards.Elements[2]).Value);

            var shield = (ParsedObject)dict.Entries[2];
            var shieldRewards = (ParsedArray)shield.Children[1];
            Assert.AreEqual(1, shieldRewards.Elements.Count);
        }

        [Test]
        public void VerticalArray_NestedInNested_ItemSkillsLevels()
        {
            // The canonical example from the design document
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

            // A  B       C  D   E  F   G
            // 1  Sword   0  10  0  1   100
            //                   1  5   200
            //                   2  10  350
            //            1  11  0  1   80
            //                   1  3   150
            // 2  Shield  0  20  0  1   50
            //                   1  4   120
            var data = MakeTable(
                new[] { "ID", "Name", "SklIdx", "SklID", "LvlIdx", "LvlReq", "LvlPow" },
                new[] { "1", "Sword", "0", "10", "0", "1", "100" },
                new[] { "", "", "", "", "1", "5", "200" },
                new[] { "", "", "", "", "2", "10", "350" },
                new[] { "", "", "1", "11", "0", "1", "80" },
                new[] { "", "", "", "", "1", "3", "150" },
                new[] { "2", "Shield", "0", "20", "0", "1", "50" },
                new[] { "", "", "", "", "1", "4", "120" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            Assert.AreEqual(2, dict.Entries.Count);

            // Sword
            var sword = (ParsedObject)dict.Entries[1];
            Assert.AreEqual("Sword", ((ParsedValue)sword.Children[0]).Value);

            var skills = (ParsedArray)sword.Children[1];
            Assert.AreEqual(2, skills.Elements.Count);

            // Skill 0 (id=10) has 3 levels
            var skill0 = (ParsedObject)skills.Elements[0];
            Assert.AreEqual(10, ((ParsedValue)skill0.Children[0]).Value);
            var skill0Levels = (ParsedArray)skill0.Children[1];
            Assert.AreEqual(3, skill0Levels.Elements.Count);
            var lvl0 = (ParsedObject)skill0Levels.Elements[0];
            Assert.AreEqual(100, ((ParsedValue)lvl0.Children[1]).Value);
            var lvl2 = (ParsedObject)skill0Levels.Elements[2];
            Assert.AreEqual(350, ((ParsedValue)lvl2.Children[1]).Value);

            // Skill 1 (id=11) has 2 levels
            var skill1 = (ParsedObject)skills.Elements[1];
            Assert.AreEqual(11, ((ParsedValue)skill1.Children[0]).Value);
            var skill1Levels = (ParsedArray)skill1.Children[1];
            Assert.AreEqual(2, skill1Levels.Elements.Count);

            // Shield
            var shield = (ParsedObject)dict.Entries[2];
            var shieldSkills = (ParsedArray)shield.Children[1];
            Assert.AreEqual(1, shieldSkills.Elements.Count);
            var shieldSkill0 = (ParsedObject)shieldSkills.Elements[0];
            var shieldLevels = (ParsedArray)shieldSkill0.Children[1];
            Assert.AreEqual(2, shieldLevels.Elements.Count);
        }

        [Test]
        public void NestedObjectField_ParsesCorrectly()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableObjectField("stats", "ItemStats", new ParseableNode[]
                    {
                        new ParseableCustomField("hp", "B", FieldValueType.Int),
                        new ParseableCustomField("atk", "C", FieldValueType.Float),
                    }),
                }))
                .Build();

            var data = MakeTable(
                new[] { "ID", "HP", "ATK" },
                new[] { "1", "100", "25.5" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            var item = (ParsedObject)dict.Entries[1];
            var stats = (ParsedObject)item.Children[0];
            Assert.AreEqual("ItemStats", stats.TypeName);
            Assert.AreEqual(100, ((ParsedValue)stats.Children[0]).Value);
            Assert.AreEqual(25.5f, (float)((ParsedValue)stats.Children[1]).Value, 0.001f);
        }

        [Test]
        public void DictionaryField_ParsesCorrectly()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                    new ParseableDictionaryField("props", "Prop", "C", KeyType.String, new ParseableNode[]
                    {
                        new ParseableCustomField("value", "D", FieldValueType.String),
                    }),
                }))
                .Build();

            // A  B      C        D
            // 1  Sword  damage   high
            //           speed    medium
            //           rarity   rare
            // 2  Shield defense  strong
            var data = MakeTable(
                new[] { "ID", "Name", "PropKey", "PropValue" },
                new[] { "1", "Sword", "damage", "high" },
                new[] { "", "", "speed", "medium" },
                new[] { "", "", "rarity", "rare" },
                new[] { "2", "Shield", "defense", "strong" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            Assert.AreEqual(2, dict.Entries.Count);

            var sword = (ParsedObject)dict.Entries[1];
            var props = (ParsedDictionary)sword.Children[1];
            Assert.AreEqual(3, props.Entries.Count);
            Assert.IsTrue(props.Entries.ContainsKey("damage"));
            Assert.IsTrue(props.Entries.ContainsKey("rarity"));
        }

        [Test]
        public void TypeMismatch_ProducesWarningAndDefault()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("damage", "B", FieldValueType.Int),
                }))
                .Build();

            var data = MakeTable(
                new[] { "ID", "Damage" },
                new[] { "1", "not_a_number" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Value.ParseWarnings.Count > 0);

            var dict = (ParsedDictionary)result.Value.Root;
            var item = (ParsedObject)dict.Entries[1];
            var damage = (ParsedValue)item.Children[0];
            Assert.AreEqual(0, damage.Value);
        }

        [Test]
        public void DuplicateKey_SkipsAndWarns()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                }))
                .Build();

            var data = MakeTable(
                new[] { "ID", "Name" },
                new[] { "1", "Sword" },
                new[] { "1", "DuplicateSword" },
                new[] { "2", "Shield" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            Assert.AreEqual(2, dict.Entries.Count);

            var item1 = (ParsedObject)dict.Entries[1];
            Assert.AreEqual("Sword", ((ParsedValue)item1.Children[0]).Value);

            Assert.IsTrue(result.Value.ParseWarnings.Any(
                w => w.Message.Contains("Duplicate")));
        }

        [Test]
        public void EmptyTable_DictionaryRoot_ReturnsEmptyDictionary()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableCustomField("name", "B", FieldValueType.String),
                }))
                .Build();

            var data = MakeTable(new[] { "ID", "Name" });

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            Assert.AreEqual(0, dict.Entries.Count);
        }

        [Test]
        public void NullData_ReturnsFailure()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Test")
                .WithRoot(new ParseableObjectRoot("Cfg", new ParseableNode[]
                {
                    new ParseableCustomField("x", "A", FieldValueType.Int),
                }))
                .Build();

            var result = _engine.Parse(null, graph);

            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void NullGraph_ReturnsFailure()
        {
            var data = MakeTable(new[] { "A" }, new[] { "1" });

            var result = _engine.Parse(data, null);

            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void AssetField_ReturnsPathAsString()
        {
            var graph = new ParseableGraphBuilder()
                .WithGraphName("Items")
                .WithRoot(new ParseableDictionaryRoot("Item", "A", KeyType.Int, new ParseableNode[]
                {
                    new ParseableAssetField("icon", "B", "Sprite", AssetLoadMethod.Addressables),
                }))
                .Build();

            var data = MakeTable(
                new[] { "ID", "Icon" },
                new[] { "1", "sprites/sword_icon" }
            );

            var result = _engine.Parse(data, graph);

            Assert.IsTrue(result.IsSuccess);
            var dict = (ParsedDictionary)result.Value.Root;
            var item = (ParsedObject)dict.Entries[1];
            var icon = (ParsedValue)item.Children[0];
            Assert.AreEqual("sprites/sword_icon", icon.Value);
        }

        private static RawTableData MakeTable(string[] headers, params string[][] rows)
        {
            return new RawTableData(rows, headers);
        }
    }
}
