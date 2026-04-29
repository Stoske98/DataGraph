using System;
using NUnit.Framework;
using DataGraph.Editor.Domain;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class RootTypeResolverTests
    {
        [Test]
        public void GetTypeName_ObjectRoot_ReturnsTypeName()
        {
            var root = new ParseableObjectRoot("Hero", Array.Empty<ParseableNode>());

            Assert.AreEqual("Hero", RootTypeResolver.GetTypeName(root));
        }

        [Test]
        public void GetTypeName_ArrayRoot_ReturnsTypeName()
        {
            var root = new ParseableArrayRoot("Level",
                Array.Empty<ParseableNode>());

            Assert.AreEqual("Level", RootTypeResolver.GetTypeName(root));
        }

        [Test]
        public void GetTypeName_DictionaryRoot_ReturnsTypeName()
        {
            var root = new ParseableDictionaryRoot("Item", "A", KeyType.Int,
                Array.Empty<ParseableNode>());

            Assert.AreEqual("Item", RootTypeResolver.GetTypeName(root));
        }

        [Test]
        public void GetTypeName_UnknownRoot_Throws()
        {
            var fake = new FakeRoot();

            Assert.Throws<InvalidOperationException>(() =>
                RootTypeResolver.GetTypeName(fake));
        }

        private sealed class FakeRoot : ParseableNode
        {
            public FakeRoot() : base(null, Array.Empty<ParseableNode>()) { }
        }
    }
}
