using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Error.ErrorCode must read both code styles that reach Unity: enum names
    /// raised in C# and the jslib, and the Core runtime's SCREAMING_SNAKE codes.
    /// </summary>
    public class ErrorCodeTests
    {
        private static ErrorCode Read(string code)
        {
            return new Error { Code = code }.ErrorCode;
        }

        [TestCase("PlatformError", ErrorCode.PlatformError)]
        [TestCase("platformerror", ErrorCode.PlatformError)]
        [TestCase("FeatureNotSupported", ErrorCode.FeatureNotSupported)]
        [TestCase("UserCancelled", ErrorCode.UserCancelled)]
        [TestCase("Timeout", ErrorCode.Timeout)]
        public void EnumNames_Parse(string code, ErrorCode expected)
        {
            Assert.AreEqual(expected, Read(code));
        }

        [TestCase("NOT_INITIALIZED", ErrorCode.NotInitialized)]
        [TestCase("INVALID_PARAM", ErrorCode.InvalidParams)]
        [TestCase("FEATURE_NOT_SUPPORTED", ErrorCode.FeatureNotSupported)]
        [TestCase("PLATFORM_ERROR", ErrorCode.PlatformError)]
        [TestCase("ADS_BLOCKED", ErrorCode.PlatformError)]
        [TestCase("NETWORK_FAILURE", ErrorCode.NetworkError)]
        [TestCase("ADS_FREQUENCY_LIMITED", ErrorCode.RateLimited)]
        [TestCase("TIMEOUT", ErrorCode.Timeout)]
        [TestCase("UNKNOWN_ERROR", ErrorCode.Unknown)]
        public void CoreCodes_MapToTheMatchingErrorCode(string code, ErrorCode expected)
        {
            Assert.AreEqual(expected, Read(code));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("NoFill")]
        [TestCase("SOMETHING_NEW")]
        [TestCase("3")]
        [TestCase("99")]
        public void UnrecognisedCodes_ReadAsUnknown(string code)
        {
            Assert.AreEqual(ErrorCode.Unknown, Read(code));
        }
    }
}
