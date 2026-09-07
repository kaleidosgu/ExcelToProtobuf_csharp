using HiProtobuf.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HiProtobuf.Test
{
    [TestClass]
    public class LanguageGeneraterTests
    {
        [TestMethod]
        public void NormalizeLineEndingsToCRLF_NormalizesAllLineEndingStyles()
        {
            const string input = "first\nsecond\r\nthird\rfourth";

            var result = LanguageGenerater.NormalizeLineEndingsToCRLF(input);

            Assert.AreEqual("first\r\nsecond\r\nthird\r\nfourth", result);
        }

        [TestMethod]
        public void NormalizeLineEndingsToCRLF_IsIdempotent()
        {
            const string input = "first\r\nsecond\r\n";

            var result = LanguageGenerater.NormalizeLineEndingsToCRLF(input);

            Assert.AreEqual(input, result);
        }
    }
}
