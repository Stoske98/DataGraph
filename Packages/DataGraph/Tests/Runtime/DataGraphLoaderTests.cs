using System;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    /// <summary>
    /// Verifies that per-loader unregistration does not affect entries
    /// registered by other loaders. Tests simulate the DataGraphLoader
    /// lifecycle (register on awake, unregister on destroy) by calling
    /// Database directly, which is the same path exercised at runtime.
    /// </summary>
    [TestFixture]
    public class DataGraphLoaderTests
    {
        [SetUp]
        public void SetUp()
        {
            Database.Clear();
        }

        [Test]
        public void DestroyOneLoader_DoesNotClearOtherLoadersData()
        {
            var dbX = new ArrayDatabase<LoaderStubX>(new[] { new LoaderStubX(1) });
            var dbY = new ArrayDatabase<LoaderStubY>(new[] { new LoaderStubY(2) });

            Database.Register(dbX);
            var loaderAEntries = new[] { typeof(LoaderStubX) };

            Database.Register(dbY);

            foreach (var t in loaderAEntries)
                Database.Unregister(t);

            Assert.IsFalse(Database.IsRegistered<LoaderStubX>(),
                "LoaderA's entry should be gone after its simulated OnDestroy.");
            Assert.IsTrue(Database.IsRegistered<LoaderStubY>(),
                "LoaderB's entry must survive LoaderA's OnDestroy.");
            Assert.AreEqual(2, Database.Get<LoaderStubY>().GetByIndex(0).Value);
        }

        [Test]
        public void DestroyLoader_OnlyUnregistersOwnEntries()
        {
            var dbX = new ArrayDatabase<LoaderStubX>(new[] { new LoaderStubX(10) });
            var dbY = new ArrayDatabase<LoaderStubY>(new[] { new LoaderStubY(20) });
            var dbZ = new ArrayDatabase<LoaderStubZ>(new[] { new LoaderStubZ(30) });

            Database.Register(dbX);
            Database.Register(dbY);
            var loaderAEntries = new[] { typeof(LoaderStubX), typeof(LoaderStubY) };

            Database.Register(dbZ);

            foreach (var t in loaderAEntries)
                Database.Unregister(t);

            Assert.IsFalse(Database.IsRegistered<LoaderStubX>(), "X must be gone (registered by A).");
            Assert.IsFalse(Database.IsRegistered<LoaderStubY>(), "Y must be gone (registered by A).");
            Assert.IsTrue(Database.IsRegistered<LoaderStubZ>(), "Z must survive (registered by B).");
            Assert.AreEqual(30, Database.Get<LoaderStubZ>().GetByIndex(0).Value);
        }

        [Test]
        public void SameLoaderDestroyedTwice_DoesNotThrow()
        {
            var dbX = new ArrayDatabase<LoaderStubX>(new[] { new LoaderStubX(1) });
            Database.Register(dbX);
            var loaderAEntries = new[] { typeof(LoaderStubX) };

            Assert.DoesNotThrow(() =>
            {
                foreach (var t in loaderAEntries)
                    Database.Unregister(t);
            }, "First OnDestroy must not throw.");

            Assert.DoesNotThrow(() =>
            {
                foreach (var t in loaderAEntries)
                    Database.Unregister(t);
            }, "Second OnDestroy (idempotent unregister) must not throw.");
        }

        [Test]
        public void Unregister_NotRegisteredType_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Database.Unregister(typeof(LoaderStubX)),
                "Unregistering a type that was never registered must be a silent no-op.");
        }

        [Test]
        public void Unregister_LeavesOtherTypesIntact()
        {
            var dbX = new ArrayDatabase<LoaderStubX>(new[] { new LoaderStubX(5) });
            var dbY = new ArrayDatabase<LoaderStubY>(new[] { new LoaderStubY(6) });
            Database.Register(dbX);
            Database.Register(dbY);

            Database.Unregister(typeof(LoaderStubX));

            Assert.IsFalse(Database.IsRegistered<LoaderStubX>());
            Assert.IsTrue(Database.IsRegistered<LoaderStubY>());
        }

        private class LoaderStubX
        {
            public int Value { get; }
            public LoaderStubX(int value) => Value = value;
        }

        private class LoaderStubY
        {
            public int Value { get; }
            public LoaderStubY(int value) => Value = value;
        }

        private class LoaderStubZ
        {
            public int Value { get; }
            public LoaderStubZ(int value) => Value = value;
        }
    }
}
