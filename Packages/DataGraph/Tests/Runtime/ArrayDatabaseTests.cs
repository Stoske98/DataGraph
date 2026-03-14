using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class ArrayDatabaseTests
    {
        [Test]
        public void GetByIndex_ReturnsCorrectElement()
        {
            var db = MakeArray("A", "B", "C");

            Assert.AreEqual("A", db.GetByIndex(0));
            Assert.AreEqual("B", db.GetByIndex(1));
            Assert.AreEqual("C", db.GetByIndex(2));
        }

        [Test]
        public void GetByIndex_NegativeIndex_Throws()
        {
            var db = MakeArray("A");

            Assert.Throws<ArgumentOutOfRangeException>(() => db.GetByIndex(-1));
        }

        [Test]
        public void GetByIndex_IndexTooLarge_Throws()
        {
            var db = MakeArray("A", "B");

            Assert.Throws<ArgumentOutOfRangeException>(() => db.GetByIndex(2));
        }

        [Test]
        public void GetAll_ReturnsAllInOrder()
        {
            var db = MakeArray("X", "Y", "Z");

            var all = db.GetAll().ToList();

            CollectionAssert.AreEqual(new[] { "X", "Y", "Z" }, all);
        }

        [Test]
        public void Count_ReturnsEntryCount()
        {
            var db = MakeArray("A", "B", "C");

            Assert.AreEqual(3, db.Count);
        }

        [Test]
        public void EmptyArray_CountIsZero()
        {
            var db = MakeArray();

            Assert.AreEqual(0, db.Count);
            Assert.IsEmpty(db.GetAll());
        }

        private static ArrayDatabase<string> MakeArray(params string[] items)
            => new(items);
    }
}
