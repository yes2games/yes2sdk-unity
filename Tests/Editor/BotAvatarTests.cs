using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>Covers Player.GetBotAvatarAsync argument checks, the bridge callbacks and the Editor mock.</summary>
    public class BotAvatarTests
    {
        private bool _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = Yes2SDKEditorMock.PlatformServicesEnabled;
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = _saved;
        }

        [TestCase("small")]
        [TestCase("medium")]
        [TestCase("large")]
        public void TryValidate_AcceptsEachSize(string size)
        {
            Assert.IsTrue(Yes2SDKPlayer.TryValidateBotAvatarArgs("RoboRita", size, out string message), message);
        }

        private static IEnumerable<TestCaseData> InvalidArgs()
        {
            yield return new TestCaseData(null, "medium", "username must be a non-empty string").SetName("Null username");
            yield return new TestCaseData("", "medium", "username must be a non-empty string").SetName("Empty username");
            yield return new TestCaseData("  ", "medium", "username must be a non-empty string").SetName("Blank username");
            yield return new TestCaseData("bot", null, "size must be \"small\", \"medium\" or \"large\"").SetName("Null size");
            yield return new TestCaseData("bot", "Medium", "size must be \"small\", \"medium\" or \"large\"").SetName("Wrong case size");
            yield return new TestCaseData("bot", "huge", "size must be \"small\", \"medium\" or \"large\"").SetName("Unknown size");
        }

        [TestCaseSource(nameof(InvalidArgs))]
        public void InvalidArgs_FailSynchronouslyWithInvalidParams(string username, string size, string expected)
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = true;
            var errors = new List<Error>();

            new Yes2SDKPlayer().GetBotAvatarAsync(username, size, _ => Assert.Fail("success"), errors.Add);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(ErrorCode.InvalidParams, errors[0].ErrorCode);
            Assert.AreEqual(expected, errors[0].Message);
            Assert.AreEqual("Yes2SDK.Player.GetBotAvatarAsync", errors[0].Context);
        }

        [Test]
        public void ServicesEnabled_DeliversAMockUrlForTheDefaultSize()
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = true;
            string url = null;

            new Yes2SDKPlayer().GetBotAvatarAsync("Robo Rita", u => url = u, e => Assert.Fail(e.Message));

            Assert.AreEqual(Yes2SDKPlayer.MockBotAvatarUrl("Robo Rita", "medium"), url);
            StringAssert.Contains("/medium/Robo%20Rita", url);
            Assert.IsTrue(new Yes2SDKPlayer().IsBotAvatarSupported());
        }

        [Test]
        public void ServicesEnabled_SameUsernameGivesTheSameUrl()
        {
            Assert.AreEqual(Yes2SDKPlayer.MockBotAvatarUrl("bot", "small"), Yes2SDKPlayer.MockBotAvatarUrl("bot", "small"));
            Assert.AreNotEqual(Yes2SDKPlayer.MockBotAvatarUrl("bot", "small"), Yes2SDKPlayer.MockBotAvatarUrl("bot", "large"));
        }

        [Test]
        public void ServicesDisabled_ReportsFeatureNotSupported()
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = false;
            string url = null;
            Error received = default;

            new Yes2SDKPlayer().GetBotAvatarAsync("bot", "small", u => url = u, e => received = e);

            Assert.IsNull(url);
            Assert.AreEqual("FeatureNotSupported", received.Code);
            Assert.IsFalse(new Yes2SDKPlayer().IsBotAvatarSupported());
        }

        [Test]
        public void TaskOverloads_ResolveAndFault()
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = true;
            var player = new Yes2SDKPlayer();

            var ok = player.GetBotAvatarAsync("bot", CancellationToken.None);
            var sized = player.GetBotAvatarAsync("bot", "large", CancellationToken.None);
            var invalid = player.GetBotAvatarAsync("", CancellationToken.None);

            Assert.IsTrue(ok.IsCompleted && !ok.IsFaulted);
            Assert.AreEqual(Yes2SDKPlayer.MockBotAvatarUrl("bot", "medium"), ok.Result);
            Assert.AreEqual(Yes2SDKPlayer.MockBotAvatarUrl("bot", "large"), sized.Result);
            Assert.IsTrue(invalid.IsFaulted);
            Assert.AreEqual(ErrorCode.InvalidParams, ((Yes2SDKException)invalid.Exception.InnerException).ErrorCode);
        }

        [Test]
        public void BridgeCallbacks_FireOnceAndAreCleared()
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = true;
            var calls = new List<string>();
            new Yes2SDKPlayer().GetBotAvatarAsync("bot", u => calls.Add("ok:" + u), e => calls.Add("err:" + e.Code));
            calls.Clear();

            Yes2SDKPlayer.InvokeGetBotAvatarSuccess("late");
            Yes2SDKPlayer.InvokeGetBotAvatarError(new Error { Code = "Late" });

            Assert.IsEmpty(calls);
        }
    }
}
