using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class DatabaseTests
    {
        [SetUp]
        public void SetUp()
        {
            Database.Clear();
        }

        // --- Registration and retrieval ---

        [Test]
        public void Register_IntDictionary_GetById_Works()
        {
            var db = new DictionaryDatabase<int, ItemStub>(new()
            {
                { 1, new ItemStub("Sword") },
                { 2, new ItemStub("Shield") },
            });
            Database.Register(db);

            var sword = Database.Get<ItemStub>().GetById(1);

            Assert.AreEqual("Sword", sword.Name);
        }

        [Test]
        public void Register_StringDictionary_GetById_Works()
        {
            var db = new DictionaryDatabase<string, LocaleStub>(new()
            {
                { "en", new LocaleStub("Hello") },
                { "sr", new LocaleStub("Zdravo") },
            });
            Database.Register(db);

            var text = Database.Get<LocaleStub>().GetById("sr");

            Assert.AreEqual("Zdravo", text.Text);
        }

        [Test]
        public void Register_Array_GetByIndex_Works()
        {
            var db = new ArrayDatabase<LevelStub>(new[]
            {
                new LevelStub(1), new LevelStub(2), new LevelStub(3)
            });
            Database.Register(db);

            var level = Database.Get<LevelStub>().GetByIndex(0);

            Assert.AreEqual(1, level.Number);
        }

        [Test]
        public void Register_Object_GetObject_Works()
        {
            var config = new ConfigStub(60, true);
            var db = new ObjectDatabase<ConfigStub>(config);
            Database.Register(db);

            var result = Database.Get<ConfigStub>().GetObject();

            Assert.AreEqual(60, result.Fps);
            Assert.IsTrue(result.VSync);
        }

        // --- IsRegistered ---

        [Test]
        public void IsRegistered_Registered_ReturnsTrue()
        {
            Database.Register(new ArrayDatabase<LevelStub>(new[] { new LevelStub(1) }));

            Assert.IsTrue(Database.IsRegistered<LevelStub>());
        }

        [Test]
        public void IsRegistered_NotRegistered_ReturnsFalse()
        {
            Assert.IsFalse(Database.IsRegistered<ItemStub>());
        }

        // --- Not found ---

        [Test]
        public void Get_NotRegistered_ThrowsDatabaseNotFound()
        {
            Assert.Throws<DatabaseNotFoundException>(() => Database.Get<ItemStub>());
        }

        // --- Clear ---

        [Test]
        public void Clear_RemovesAllRegistrations()
        {
            Database.Register(new ArrayDatabase<LevelStub>(new[] { new LevelStub(1) }));
            Database.Register(new ObjectDatabase<ConfigStub>(new ConfigStub(30, false)));

            Database.Clear();

            Assert.IsFalse(Database.IsRegistered<LevelStub>());
            Assert.IsFalse(Database.IsRegistered<ConfigStub>());
        }

        // --- Re-registration overwrites ---

        [Test]
        public void Register_SameType_Twice_OverwritesPrevious()
        {
            Database.Register(new ObjectDatabase<ConfigStub>(new ConfigStub(30, false)));
            Database.Register(new ObjectDatabase<ConfigStub>(new ConfigStub(60, true)));

            var result = Database.Get<ConfigStub>().GetObject();

            Assert.AreEqual(60, result.Fps);
        }

        // --- Kind mismatch ---

        [Test]
        public void Handle_GetById_OnArray_ThrowsKindMismatch()
        {
            Database.Register(new ArrayDatabase<ItemStub>(new[] { new ItemStub("Sword") }));

            Assert.Throws<DatabaseKindMismatchException>(
                () => Database.Get<ItemStub>().GetById(1));
        }

        [Test]
        public void Handle_GetByIndex_OnDictionary_ThrowsKindMismatch()
        {
            Database.Register(new DictionaryDatabase<int, ItemStub>(new()
            {
                { 1, new ItemStub("Sword") }
            }));

            Assert.Throws<DatabaseKindMismatchException>(
                () => Database.Get<ItemStub>().GetByIndex(0));
        }

        [Test]
        public void Handle_GetObject_OnArray_ThrowsKindMismatch()
        {
            Database.Register(new ArrayDatabase<ConfigStub>(new[] { new ConfigStub(60, true) }));

            Assert.Throws<DatabaseKindMismatchException>(
                () => Database.Get<ConfigStub>().GetObject());
        }

        [Test]
        public void Handle_GetById_String_OnIntDictionary_ThrowsKindMismatch()
        {
            Database.Register(new DictionaryDatabase<int, ItemStub>(new()
            {
                { 1, new ItemStub("Sword") }
            }));

            Assert.Throws<DatabaseKindMismatchException>(
                () => Database.Get<ItemStub>().GetById("sword"));
        }

        // --- Full API access ---

        [Test]
        public void Handle_AsDictionary_ReturnsFullApi()
        {
            Database.Register(new DictionaryDatabase<int, ItemStub>(new()
            {
                { 1, new ItemStub("Sword") },
                { 2, new ItemStub("Shield") },
            }));

            var dictDb = Database.Get<ItemStub>().AsDictionary();

            Assert.AreEqual(2, dictDb.Count);
            Assert.IsTrue(dictDb.TryGetById(1, out var sword));
            Assert.AreEqual("Sword", sword.Name);
            Assert.IsTrue(dictDb.ContainsKey(2));
        }

        [Test]
        public void Handle_AsArray_ReturnsFullApi()
        {
            Database.Register(new ArrayDatabase<LevelStub>(new[]
            {
                new LevelStub(1), new LevelStub(2)
            }));

            var arrDb = Database.Get<LevelStub>().AsArray();

            Assert.AreEqual(2, arrDb.Count);
            Assert.AreEqual(1, arrDb.GetByIndex(0).Number);
        }

        [Test]
        public void Handle_AsObject_ReturnsFullApi()
        {
            Database.Register(new ObjectDatabase<ConfigStub>(new ConfigStub(120, false)));

            var objDb = Database.Get<ConfigStub>().AsObject();

            Assert.AreEqual(120, objDb.Get().Fps);
        }

        // --- GetAll across kinds ---

        [Test]
        public void Handle_GetAll_IntDictionary_ReturnsAllValues()
        {
            Database.Register(new DictionaryDatabase<int, ItemStub>(new()
            {
                { 1, new ItemStub("A") }, { 2, new ItemStub("B") }
            }));

            var all = Database.Get<ItemStub>().GetAll().ToList();

            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void Handle_GetAll_StringDictionary_ReturnsAllValues()
        {
            Database.Register(new DictionaryDatabase<string, LocaleStub>(new()
            {
                { "en", new LocaleStub("Hi") }, { "sr", new LocaleStub("Cao") }
            }));

            var all = Database.Get<LocaleStub>().GetAll().ToList();

            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void Handle_GetAll_Array_ReturnsAllInOrder()
        {
            Database.Register(new ArrayDatabase<LevelStub>(new[]
            {
                new LevelStub(1), new LevelStub(2), new LevelStub(3)
            }));

            var all = Database.Get<LevelStub>().GetAll().ToList();

            Assert.AreEqual(3, all.Count);
            Assert.AreEqual(1, all[0].Number);
            Assert.AreEqual(3, all[2].Number);
        }

        [Test]
        public void Handle_GetAll_Object_ReturnsSingleElement()
        {
            Database.Register(new ObjectDatabase<ConfigStub>(new ConfigStub(60, true)));

            var all = Database.Get<ConfigStub>().GetAll().ToList();

            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(60, all[0].Fps);
        }

        // --- Test stubs ---

        private class ItemStub
        {
            public string Name { get; }
            public ItemStub(string name) => Name = name;
        }

        private class LevelStub
        {
            public int Number { get; }
            public LevelStub(int number) => Number = number;
        }

        private class ConfigStub
        {
            public int Fps { get; }
            public bool VSync { get; }
            public ConfigStub(int fps, bool vsync) { Fps = fps; VSync = vsync; }
        }

        private class LocaleStub
        {
            public string Text { get; }
            public LocaleStub(string text) => Text = text;
        }
    }
}
