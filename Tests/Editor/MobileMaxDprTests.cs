using NUnit.Framework;
using Yes2SDK.Editor;

namespace Yes2SDK.Tests
{
    /// <summary>Covers which MOBILE_MAX_DPR template values the build guard accepts.</summary>
    public class MobileMaxDprTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("1.4")]
        [TestCase("1")]
        [TestCase("2.25")]
        public void IsValidMobileMaxDpr_EmptyOrPositive_Accepts(string value)
        {
            Assert.IsTrue(Yes2SDKBuildGuard.IsValidMobileMaxDpr(value));
        }

        [TestCase(" ")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1,4")]
        [TestCase("abc")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void IsValidMobileMaxDpr_NotAPositiveNumber_Rejects(string value)
        {
            Assert.IsFalse(Yes2SDKBuildGuard.IsValidMobileMaxDpr(value));
        }
    }
}
