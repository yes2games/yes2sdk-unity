using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>Covers the pure helpers behind the Editor mock settings.</summary>
    public class EditorMockSettingsTests
    {
        [TestCase(-5, 0)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(20, 20)]
        [TestCase(21, 20)]
        [TestCase(1000, 20)]
        public void ClampConversions_StaysWithinZeroToTwenty(int input, int expected)
        {
            Assert.AreEqual(expected, Yes2SDKEditorMock.ClampConversions(input));
        }
    }
}
