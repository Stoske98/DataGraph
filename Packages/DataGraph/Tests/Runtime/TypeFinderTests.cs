using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class TypeFinderTests
    {
        [Test]
        public void Find_KnownType_ReturnsType()
        {
            var t = TypeFinder.Find("DataGraph.Runtime.RawTableData");

            Assert.IsNotNull(t);
            Assert.AreEqual(typeof(RawTableData), t);
        }

        [Test]
        public void Find_UnknownType_ReturnsNull()
        {
            Assert.IsNull(TypeFinder.Find("Nonexistent.Type.Name"));
        }

        [Test]
        public void Find_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(TypeFinder.Find(null));
            Assert.IsNull(TypeFinder.Find(""));
        }

        [Test]
        public void Find_SecondCall_ServesFromCache()
        {
            // Both calls should return the same Type instance.
            var first = TypeFinder.Find("DataGraph.Runtime.Result`1");
            var second = TypeFinder.Find("DataGraph.Runtime.Result`1");

            Assert.AreSame(first, second);
            Assert.IsNotNull(first);
        }

        [Test]
        public void FindGenerated_FallsBackToBareName()
        {
            // No DataGraph.Data.RawTableData exists; bare name resolves.
            var t = TypeFinder.FindGenerated("DataGraph.Runtime.RawTableData");

            Assert.IsNotNull(t);
            Assert.AreEqual(typeof(RawTableData), t);
        }

        [Test]
        public void FindGenerated_UnknownType_ReturnsNull()
        {
            Assert.IsNull(TypeFinder.FindGenerated("NoSuchGeneratedType"));
        }
    }
}
