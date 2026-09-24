using System.Collections.Generic;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the ad watchdog and request-id routing: an ad the platform never
    /// completes is released with a Timeout error, and a callback for an ad
    /// that is no longer in flight is ignored, so every ad settles exactly once.
    ///
    /// A real platform cannot stall headless, so these cases hold an ad in
    /// flight through the Begin* seam, drive a fake clock, and deliver bridge
    /// messages by hand in the shape the jslib sends them.
    /// </summary>
    public class AdWatchdogTests
    {
        private List<string> _calls;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            _now = 1000f;
            Yes2SDKAds.ResetAdStateForTests();
            Yes2SDKAds.Clock = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKAds.ResetAdStateForTests();
        }

        private int BeginInterstitial(string label = "")
        {
            return Yes2SDKAds.BeginInterstitial(
                () => _calls.Add(label + "beforeAd"),
                () => _calls.Add(label + "afterAd"),
                error => _calls.Add(label + "onError:" + error.Code));
        }

        private int BeginRewarded(string label = "")
        {
            return Yes2SDKAds.BeginRewarded(
                () => _calls.Add(label + "beforeAd"),
                () => _calls.Add(label + "afterAd"),
                () => _calls.Add(label + "adDismissed"),
                () => _calls.Add(label + "adViewed"),
                error => _calls.Add(label + "onError:" + error.Code));
        }

        private static void Send(int requestId, System.Action invoke)
        {
            Yes2SDKAds.HandleBridgeMessage(requestId.ToString(), invoke);
        }

        private static void SendError(int requestId, string code, System.Action<Error> invoke)
        {
            string json = "{\"code\":\"" + code + "\",\"message\":\"m\",\"context\":\"c\"}";
            Yes2SDKAds.HandleBridgeError(requestId + "|" + json, Bridge.ParseError, invoke);
        }

        private void Advance(float seconds)
        {
            _now += seconds;
            Yes2SDKAds.CheckAdWatchdog();
        }

        [Test]
        public void AdThatNeverStarts_IsReleasedWithATimeoutAfterTheStartDeadline()
        {
            BeginInterstitial();

            Advance(Yes2SDKAds.AdStartTimeoutSeconds - 0.1f);
            Assert.IsEmpty(_calls, "the watchdog must not fire before the deadline");
            Assert.IsTrue(Yes2SDK.Ads.IsAdShowing());

            Advance(0.2f);
            Assert.AreEqual(new[] { "onError:Timeout" }, _calls);
            Assert.IsFalse(Yes2SDK.Ads.IsAdShowing(), "the timeout must release the latch");
        }

        [Test]
        public void TimeoutError_MapsToTheTimeoutErrorCode()
        {
            ErrorCode code = ErrorCode.Unknown;
            Yes2SDKAds.BeginRewarded(null, null, null, null, error => code = error.ErrorCode);

            Advance(Yes2SDKAds.AdStartTimeoutSeconds + 1f);

            Assert.AreEqual(ErrorCode.Timeout, code);
        }

        [Test]
        public void AdOnScreen_GetsThePlayingDeadlineInsteadOfTheStartDeadline()
        {
            int ad = BeginRewarded();
            Advance(5f);
            Send(ad, Yes2SDKAds.InvokeRewardedBeforeAd);

            // A long rewarded video runs past the start deadline without being cut off.
            Advance(Yes2SDKAds.AdStartTimeoutSeconds + 30f);
            Assert.AreEqual(new[] { "beforeAd" }, _calls);
            Assert.IsTrue(Yes2SDK.Ads.IsAdShowing());

            Advance(Yes2SDKAds.AdPlayingTimeoutSeconds);
            Assert.AreEqual(new[] { "beforeAd", "onError:Timeout" }, _calls);
            Assert.IsFalse(Yes2SDK.Ads.IsAdShowing());
        }

        [Test]
        public void LateCompletionAfterTheTimeout_IsIgnored()
        {
            int ad = BeginRewarded();
            Advance(Yes2SDKAds.AdStartTimeoutSeconds + 1f);

            Send(ad, Yes2SDKAds.InvokeRewardedBeforeAd);
            Send(ad, Yes2SDKAds.InvokeRewardedAdViewed);
            Send(ad, Yes2SDKAds.InvokeRewardedAfterAd);
            SendError(ad, "NoFill", Yes2SDKAds.InvokeRewardedError);

            Assert.AreEqual(new[] { "onError:Timeout" }, _calls, "the ad settles once; nothing arrives for it afterwards");
        }

        [Test]
        public void LateCompletionAfterTheTimeout_DoesNotTouchTheNextAd()
        {
            int first = BeginInterstitial("A:");
            Advance(Yes2SDKAds.AdStartTimeoutSeconds + 1f);

            int second = BeginInterstitial("B:");
            Send(first, Yes2SDKAds.InvokeInterstitialAfterAd);
            Assert.IsTrue(Yes2SDK.Ads.IsAdShowing(), "A's late afterAd must not release B");

            Send(second, Yes2SDKAds.InvokeInterstitialBeforeAd);
            Send(second, Yes2SDKAds.InvokeInterstitialAfterAd);

            Assert.AreEqual(new[] { "A:onError:Timeout", "B:beforeAd", "B:afterAd" }, _calls);
        }

        [Test]
        public void NormalCompletion_DisarmsTheWatchdog()
        {
            int ad = BeginInterstitial();
            Send(ad, Yes2SDKAds.InvokeInterstitialBeforeAd);
            Advance(10f);
            Send(ad, Yes2SDKAds.InvokeInterstitialAfterAd);

            Advance(Yes2SDKAds.AdPlayingTimeoutSeconds * 10f);

            Assert.AreEqual(new[] { "beforeAd", "afterAd" }, _calls, "a slow but successful ad is not reported as a timeout");
        }

        [Test]
        public void ErrorThenAfterAd_SettlesOnce()
        {
            // A live platform can send afterAd after a no-fill that already settled the ad.
            int ad = BeginRewarded();

            SendError(ad, "NoFill", Yes2SDKAds.InvokeRewardedError);
            Send(ad, Yes2SDKAds.InvokeRewardedAfterAd);
            Advance(Yes2SDKAds.AdPlayingTimeoutSeconds * 2f);

            Assert.AreEqual(new[] { "onError:NoFill" }, _calls);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc")]
        [TestCase("|{}")]
        public void BridgeMessageWithoutARequestId_IsDropped(string message)
        {
            BeginInterstitial();

            Yes2SDKAds.HandleBridgeMessage(message, Yes2SDKAds.InvokeInterstitialAfterAd);

            Assert.IsEmpty(_calls);
            Assert.IsTrue(Yes2SDK.Ads.IsAdShowing());
        }

        [Test]
        public void NextAdStartedFromTheTimeoutCallback_GetsItsOwnDeadline()
        {
            Yes2SDKAds.BeginInterstitial(null, null, _ =>
            {
                _calls.Add("A:onError");
                BeginInterstitial("B:");
            });

            Advance(Yes2SDKAds.AdStartTimeoutSeconds + 1f);
            Assert.AreEqual(new[] { "A:onError" }, _calls);
            Assert.IsTrue(Yes2SDK.Ads.IsAdShowing(), "B is in flight");

            Advance(Yes2SDKAds.AdStartTimeoutSeconds - 1f);
            Assert.AreEqual(new[] { "A:onError" }, _calls, "B's deadline starts when B starts");

            Advance(2f);
            Assert.AreEqual(new[] { "A:onError", "B:onError:Timeout" }, _calls);
        }

        [Test]
        public void EditorSynchronousPath_CompletesWithNothingLeftForTheWatchdog()
        {
            Yes2SDK.Ads.ShowRewarded("test", "rewarded",
                afterAd: () => _calls.Add("afterAd"),
                onError: error => _calls.Add("onError:" + error.Code));

            Advance(Yes2SDKAds.AdPlayingTimeoutSeconds * 2f);

            Assert.AreEqual(new[] { "afterAd" }, _calls);
        }
    }
}
