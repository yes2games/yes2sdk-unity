using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>Covers the Editor mock for signed player info.</summary>
    public class SignedPlayerMockTests
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

        [Test]
        public void MockPayload_CarriesPlayerIdAndSignature()
        {
            JObject json = JObject.Parse(Yes2SDKPlayer.MockSignedPlayerInfoJson);
            Assert.AreEqual("mock-player", (string)json["playerId"]);
            Assert.AreEqual("mock-signature", (string)json["signature"]);
        }

        [Test]
        public void ServicesEnabled_DeliversMockPayload()
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = true;
            string result = null;
            bool failed = false;
            new Yes2SDKPlayer().GetSignedPlayerInfoAsync("p", json => result = json, err => { failed = true; });
            Assert.IsFalse(failed);
            Assert.AreEqual(Yes2SDKPlayer.MockSignedPlayerInfoJson, result);
        }

        [Test]
        public void ServicesDisabled_ReportsFeatureNotSupported()
        {
            Yes2SDKEditorMock.PlatformServicesEnabled = false;
            string result = null;
            bool failed = false;
            string code = null;
            new Yes2SDKPlayer().GetSignedPlayerInfoAsync("p", json => result = json, err => { failed = true; code = err.Code; });
            Assert.IsNull(result);
            Assert.IsTrue(failed);
            Assert.AreEqual("FeatureNotSupported", code);
        }
    }
}
