using NUnit.Framework;
using Yes2SDK.Editor;

namespace Yes2SDK.Tests
{
    public class MobileMaxDprTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("1.4")]
        [TestCase("1")]
        [TestCase("2.25")]
        [TestCase("4")]
        public void IsValidMobileMaxDpr_EmptyOrPlainDecimalInRange_Accepts(string value)
        {
            Assert.IsTrue(Yes2SDKBuildGuard.IsValidMobileMaxDpr(value));
        }

        [TestCase(" ")]
        [TestCase("0")]
        [TestCase("0.0")]
        [TestCase("-1")]
        [TestCase("4.5")]
        [TestCase("1,4")]
        [TestCase(" 1.4 ")]
        [TestCase(".5")]
        [TestCase("1.4\0")]
        [TestCase("1.4\n")]
        [TestCase("1e-45")]
        [TestCase("1e3")]
        [TestCase("abc")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void IsValidMobileMaxDpr_NotAPlainDecimalInRange_Rejects(string value)
        {
            Assert.IsFalse(Yes2SDKBuildGuard.IsValidMobileMaxDpr(value));
        }
    }
}
