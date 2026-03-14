using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class DictionaryDatabaseTests
    {
        [Test]
        public void GetById_IntKey_ReturnsValue()
        {
            var db = MakeIntDict(new() { { 1, "Sword" }, { 2, "Shield" } });

            Assert.AreEqual("Sword", db.GetById(1));
            Assert.AreEqual("Shield", db.GetById(2));
        }

        [Test]
        public void GetById_StringKey_ReturnsValue()
        {
            var db = MakeStringDict(new() { { "en", "Hello" }, { "sr", "Zdravo" } });

            Assert.AreEqual("Hello", db.GetById("en"));
            Assert.AreEqual("Zdravo", db.GetById("sr"));
        }

        [Test]
        public void GetById_MissingKey_ThrowsKeyNotFound()
        {
            var db = MakeIntDict(new() { { 1, "Sword" } });

            Assert.Throws<KeyNotFoundException>(() => db.GetById(99));
        }

        [Test]
        public void TryGetById_ExistingKey_ReturnsTrueAndValue()
        {
            var db = MakeIntDict(new() { { 1, "Sword" } });

            bool found = db.TryGetById(1, out var value);

            Assert.IsTrue(found);
            Assert.AreEqual("Sword", value);
        }

        [Test]
        public void TryGetById_MissingKey_ReturnsFalse()
        {
            var db = MakeIntDict(new() { { 1, "Sword" } });

            bool found = db.TryGetById(99, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void GetAll_ReturnsAllValues()
        {
            var db = MakeIntDict(new() { { 1, "A" }, { 2, "B" }, { 3, "C" } });

            var all = db.GetAll().ToList();

            Assert.AreEqual(3, all.Count);
            CollectionAssert.Contains(all, "A");
            CollectionAssert.Contains(all, "B");
            CollectionAssert.Contains(all, "C");
        }

        [Test]
        public void GetAllKeys_ReturnsAllKeys()
        {
            var db = MakeIntDict(new() { { 10, "A" }, { 20, "B" } });

            var keys = db.GetAllKeys().ToList();

            CollectionAssert.AreEquivalent(new[] { 10, 20 }, keys);
        }

        [Test]
        public void ContainsKey_ExistingKey_ReturnsTrue()
        {
            var db = MakeIntDict(new() { { 1, "Sword" } });

            Assert.IsTrue(db.ContainsKey(1));
        }

        [Test]
        public void ContainsKey_MissingKey_ReturnsFalse()
        {
            var db = MakeIntDict(new() { { 1, "Sword" } });

            Assert.IsFalse(db.ContainsKey(99));
        }

        [Test]
        public void Count_ReturnsEntryCount()
        {
            var db = MakeIntDict(new() { { 1, "A" }, { 2, "B" } });

            Assert.AreEqual(2, db.Count);
        }

        [Test]
        public void EmptyDictionary_CountIsZero()
        {
            var db = MakeIntDict(new());

            Assert.AreEqual(0, db.Count);
            Assert.IsEmpty(db.GetAll());
        }

        private static DictionaryDatabase<int, string> MakeIntDict(Dictionary<int, string> data)
            => new(data);

        private static DictionaryDatabase<string, string> MakeStringDict(Dictionary<string, string> data)
            => new(data);
    }
}
