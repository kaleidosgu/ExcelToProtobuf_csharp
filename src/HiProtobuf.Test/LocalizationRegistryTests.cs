using System.Linq;
using HiProtobuf.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HiProtobuf.Test
{
    [TestClass]
    public class LocalizationRegistryTests
    {
        [TestMethod]
        public void Reconcile_PreservesUsedIds_AndReusesUnusedIds()
        {
            var registry = new LocalizationRegistry();
            registry.Load("100000", "旧文本");
            registry.Load("100001", "保留文本");

            var result = registry.Reconcile(new[] { "保留文本", "新文本" });

            Assert.AreEqual("100001", registry.GetKey("保留文本"));
            Assert.AreEqual("100000", registry.GetKey("新文本"));
            Assert.AreEqual(1, result.ReusedCount);
            Assert.AreEqual(0, result.EmptyCount);
        }

        [TestMethod]
        public void Reconcile_KeepsUnusedIdEmpty_UntilAFutureExportNeedsIt()
        {
            var registry = new LocalizationRegistry();
            registry.Load("100000", "已删除文本");
            registry.Load("100001", "保留文本");

            var firstResult = registry.Reconcile(new[] { "保留文本" });
            var emptyEntry = registry.Entries.Single(pair => pair.Key == "100000");

            Assert.AreEqual(string.Empty, emptyEntry.Value);
            Assert.AreEqual(1, firstResult.EmptyCount);

            var secondResult = registry.Reconcile(new[] { "保留文本", "以后新增的文本" });

            Assert.AreEqual("100000", registry.GetKey("以后新增的文本"));
            Assert.AreEqual(1, secondResult.ReusedCount);
            Assert.AreEqual(0, secondResult.EmptyCount);
        }

        [TestMethod]
        public void Reconcile_AllocatesANewId_WhenNoReusableIdExists()
        {
            var registry = new LocalizationRegistry();
            registry.Load("100000", "保留文本");

            var result = registry.Reconcile(new[] { "保留文本", "新增文本" });

            Assert.AreEqual("100000", registry.GetKey("保留文本"));
            Assert.AreEqual("100001", registry.GetKey("新增文本"));
            Assert.AreEqual(1, result.AllocatedCount);
        }
    }
}
