using System.Reflection;
using NUnit.Framework;
using DataGraph.Editor.Reflection;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class ReflectionCacheTests
    {
        private class Sample
        {
            public int PublicField;
            internal int InternalField;

            public int PublicMethod() => 1;
            public int Overload(int x) => x;
            public int Overload(string x) => x.Length;
        }

        [Test]
        public void GetField_PublicInstance_ReturnsField()
        {
            var cache = new ReflectionCache();

            var f = cache.GetField(typeof(Sample), "PublicField");

            Assert.IsNotNull(f);
            Assert.AreEqual("PublicField", f.Name);
        }

        [Test]
        public void GetField_MissingName_ReturnsNull()
        {
            var cache = new ReflectionCache();

            Assert.IsNull(cache.GetField(typeof(Sample), "Nope"));
        }

        [Test]
        public void GetField_TwoCalls_ReturnSameInstance()
        {
            var cache = new ReflectionCache();

            var first = cache.GetField(typeof(Sample), "PublicField");
            var second = cache.GetField(typeof(Sample), "PublicField");

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetField_NonPublicFlags_FindsInternalField()
        {
            var cache = new ReflectionCache();

            var f = cache.GetField(typeof(Sample), "InternalField",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(f);
        }

        [Test]
        public void GetField_NullType_ReturnsNullSafely()
        {
            var cache = new ReflectionCache();

            Assert.IsNull(cache.GetField(null, "PublicField"));
            Assert.IsNull(cache.GetField(typeof(Sample), null));
        }

        [Test]
        public void GetMethod_PublicInstance_ReturnsMethod()
        {
            var cache = new ReflectionCache();

            var m = cache.GetMethod(typeof(Sample), "PublicMethod");

            Assert.IsNotNull(m);
            Assert.AreEqual("PublicMethod", m.Name);
        }

        [Test]
        public void GetMethod_TwoCalls_ReturnSameInstance()
        {
            var cache = new ReflectionCache();

            var first = cache.GetMethod(typeof(Sample), "PublicMethod");
            var second = cache.GetMethod(typeof(Sample), "PublicMethod");

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetMethod_WithSignature_DisambiguatesOverloads()
        {
            var cache = new ReflectionCache();

            var intOverload = cache.GetMethod(typeof(Sample), "Overload",
                BindingFlags.Public | BindingFlags.Instance,
                new[] { typeof(int) });
            var stringOverload = cache.GetMethod(typeof(Sample), "Overload",
                BindingFlags.Public | BindingFlags.Instance,
                new[] { typeof(string) });

            Assert.IsNotNull(intOverload);
            Assert.IsNotNull(stringOverload);
            Assert.AreNotSame(intOverload, stringOverload);
            Assert.AreEqual(typeof(int), intOverload.GetParameters()[0].ParameterType);
            Assert.AreEqual(typeof(string), stringOverload.GetParameters()[0].ParameterType);
        }

        [Test]
        public void FindType_DelegatesToTypeFinder()
        {
            var cache = new ReflectionCache();

            var t = cache.FindType("DataGraph.Runtime.RawTableData");

            Assert.IsNotNull(t);
            Assert.AreEqual("RawTableData", t.Name);
        }

        [Test]
        public void FindGenerated_DelegatesToTypeFinder()
        {
            var cache = new ReflectionCache();

            // No DataGraph.Data.RawTableData exists, so falls back to bare name.
            var t = cache.FindGenerated("DataGraph.Runtime.RawTableData");

            Assert.IsNotNull(t);
        }
    }
}
