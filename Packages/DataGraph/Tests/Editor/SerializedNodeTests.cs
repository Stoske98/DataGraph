using NUnit.Framework;
using DataGraph.Editor;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class SerializedNodeTests
    {
        [Test]
        public void GetProperty_MissingKey_ReturnsDefault()
        {
            var node = new SerializedNode();

            Assert.AreEqual("fallback", node.GetProperty("missing", "fallback"));
            Assert.AreEqual("", node.GetProperty("missing"));
        }

        [Test]
        public void SetProperty_NewKey_AppearsInListAndGetProperty()
        {
            var node = new SerializedNode();
            node.SetProperty("Column", "B");

            Assert.AreEqual("B", node.GetProperty("Column"));
            Assert.AreEqual(1, node.Properties.Count);
            Assert.AreEqual("Column", node.Properties[0].Key);
            Assert.AreEqual("B", node.Properties[0].Value);
        }

        [Test]
        public void SetProperty_ExistingKey_UpdatesValueInPlace()
        {
            var node = new SerializedNode();
            node.SetProperty("Column", "A");
            node.SetProperty("Column", "B");

            Assert.AreEqual("B", node.GetProperty("Column"));
            Assert.AreEqual(1, node.Properties.Count, "Same-key Set must not duplicate the entry.");
        }

        [Test]
        public void SetProperty_ManyKeys_AllReadable()
        {
            var node = new SerializedNode();
            for (int i = 0; i < 50; i++)
                node.SetProperty($"k{i}", $"v{i}");

            for (int i = 0; i < 50; i++)
                Assert.AreEqual($"v{i}", node.GetProperty($"k{i}"));

            Assert.AreEqual(50, node.Properties.Count);
        }

        [Test]
        public void GetProperty_AfterDirectListPopulation_StillReadsCorrectly()
        {
            // Simulates what Unity does after deserialization: _properties is
            // already populated, _propertyCache is null, first GetProperty must
            // build the cache from the list.
            var node = new SerializedNode();
            node.Properties.Add(new SerializedNodeProperty { Key = "TypeName", Value = "Hero" });
            node.Properties.Add(new SerializedNodeProperty { Key = "Column", Value = "C" });

            Assert.AreEqual("Hero", node.GetProperty("TypeName"));
            Assert.AreEqual("C", node.GetProperty("Column"));
            Assert.AreEqual("default", node.GetProperty("missing", "default"));
        }

        [Test]
        public void SetProperty_AfterDirectListPopulation_OverwritesCorrectly()
        {
            // Cache must be populated from the list on first access, so a
            // subsequent SetProperty for an existing key updates the entry
            // rather than appending a duplicate.
            var node = new SerializedNode();
            node.Properties.Add(new SerializedNodeProperty { Key = "Column", Value = "A" });

            node.SetProperty("Column", "B");

            Assert.AreEqual("B", node.GetProperty("Column"));
            Assert.AreEqual(1, node.Properties.Count);
        }
    }
}
