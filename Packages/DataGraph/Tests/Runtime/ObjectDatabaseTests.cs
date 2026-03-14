using System;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class ObjectDatabaseTests
    {
        [Test]
        public void Get_ReturnsStoredObject()
        {
            var config = new TestConfig { MaxPlayers = 4, GameMode = "survival" };
            var db = new ObjectDatabase<TestConfig>(config);

            var result = db.Get();

            Assert.AreEqual(4, result.MaxPlayers);
            Assert.AreEqual("survival", result.GameMode);
        }

        [Test]
        public void Get_ReturnsSameInstance()
        {
            var config = new TestConfig { MaxPlayers = 8 };
            var db = new ObjectDatabase<TestConfig>(config);

            Assert.AreSame(config, db.Get());
        }

        [Test]
        public void Constructor_NullData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectDatabase<TestConfig>(null));
        }

        private class TestConfig
        {
            public int MaxPlayers { get; set; }
            public string GameMode { get; set; }
        }
    }
}
