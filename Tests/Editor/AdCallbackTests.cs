using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the ad callback contract on the Editor mock's synchronous path.
    ///
    /// The synchronous path is the only one that runs headless: the interactive
    /// popup needs Play Mode in a visible editor, so an EditMode or batch run
    /// falls through to the same entry points the WebGL bridge calls. That makes
    /// callback order and in-flight teardown testable without a test-only seam.
    /// </summary>
    public class AdCallbackTests
    {
        private List<string> _calls;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            // Ad state is static. Without this, a case that leaves an ad latched
            // on makes every later case fail for a reason that is not its own.
            Yes2SDKAds.ResetAdStateForTests();
        }

        [Test]
        public void Interstitial_InvokesBeforeAdThenAfterAd()
        {
            Yes2SDK.Ads.ShowInterstitial("test", "interstitial",
                beforeAd: () => _calls.Add("beforeAd"),
                afterAd: () => _calls.Add("afterAd"));

            Assert.AreEqual(new[] { "beforeAd", "afterAd" }, _calls);
        }

        [Test]
        public void Interstitial_ReleasesInFlightBeforeAfterAdRuns()
        {
            var inFlightDuringBeforeAd = false;
            var inFlightDuringAfterAd = true;

            Yes2SDK.Ads.ShowInterstitial("test", "interstitial",
                beforeAd: () => inFlightDuringBeforeAd = Yes2SDK.Ads.IsAdShowing(),
                afterAd: () => inFlightDuringAfterAd = Yes2SDK.Ads.IsAdShowing());

            Assert.IsTrue(inFlightDuringBeforeAd, "the ad should be in flight while beforeAd runs");
            Assert.IsFalse(inFlightDuringAfterAd, "afterAd runs after teardown, so a callback can start the next ad");
            Assert.IsFalse(Yes2SDK.Ads.IsAdShowing());
        }

        [Test]
        public void Interstitial_RejectsASecondAdWhileOneIsInFlight()
        {
            // Error is a struct, so the code is captured instead of the value,
            // to tell "no error reported" apart from a default-valued one.
            string nestedErrorCode = null;
            string nestedErrorMessage = null;

            Yes2SDK.Ads.ShowInterstitial("outer", "interstitial",
                beforeAd: () => Yes2SDK.Ads.ShowInterstitial("nested", "interstitial",
                    beforeAd: () => _calls.Add("nested-beforeAd"),
                    onError: error =>
                    {
                        nestedErrorCode = error.Code;
                        nestedErrorMessage = error.Message;
                    }),
                afterAd: () => _calls.Add("outer-afterAd"));

            Assert.IsNotNull(nestedErrorCode, "a concurrent ad call should report an error");
            Assert.AreEqual("InvalidParams", nestedErrorCode);
            Assert.That(nestedErrorMessage, Does.Contain("AdAlreadyShowing"));
            Assert.AreEqual(new[] { "outer-afterAd" }, _calls, "the nested ad must not run, the outer one must finish");
        }

        [Test]
        public void Interstitial_AThrowingAfterAdDoesNotLatchTheAdOn()
        {
            Assert.Throws<InvalidOperationException>(() =>
                Yes2SDK.Ads.ShowInterstitial("test", "interstitial",
                    afterAd: () => throw new InvalidOperationException("game code blew up")));

            Assert.IsFalse(Yes2SDK.Ads.IsAdShowing(), "a throwing callback must not block every later ad");

            Yes2SDK.Ads.ShowInterstitial("after-throw", "interstitial",
                afterAd: () => _calls.Add("afterAd"));

            Assert.AreEqual(new[] { "afterAd" }, _calls, "the next ad should run normally");
        }

        [Test]
        public void Rewarded_InvokesViewedBeforeAfterAd()
        {
            Yes2SDK.Ads.ShowRewarded("test", "rewarded",
                beforeAd: () => _calls.Add("beforeAd"),
                afterAd: () => _calls.Add("afterAd"),
                adDismissed: () => _calls.Add("adDismissed"),
                adViewed: () => _calls.Add("adViewed"));

            Assert.AreEqual(new[] { "beforeAd", "adViewed", "afterAd" }, _calls);
        }

        [Test]
        public void Rewarded_DismissDescriptionInvokesDismissedBeforeAfterAd()
        {
            Yes2SDK.Ads.ShowRewarded("test", "dismiss",
                beforeAd: () => _calls.Add("beforeAd"),
                afterAd: () => _calls.Add("afterAd"),
                adDismissed: () => _calls.Add("adDismissed"),
                adViewed: () => _calls.Add("adViewed"));

            Assert.AreEqual(new[] { "beforeAd", "adDismissed", "afterAd" }, _calls);
        }

        [Test]
        public void Rewarded_AfterAdSurvivesTheOutcomeCallbackAndRunsAfterTeardown()
        {
            var afterAdRan = false;
            var inFlightDuringAfterAd = true;

            Yes2SDK.Ads.ShowRewarded("test", "rewarded",
                afterAd: () =>
                {
                    afterAdRan = true;
                    inFlightDuringAfterAd = Yes2SDK.Ads.IsAdShowing();
                },
                adViewed: () => _calls.Add("adViewed"));

            // The outcome callback must not tear the ad down: doing so drops the
            // stored afterAd and the game never gets told to resume.
            Assert.IsTrue(afterAdRan, "afterAd must still run after the outcome callback");
            Assert.IsFalse(inFlightDuringAfterAd, "afterAd is terminal and runs after teardown");
        }

        [Test]
        public void Rewarded_ReleasesInFlightWhenNoCallbacksAreSupplied()
        {
            Yes2SDK.Ads.ShowRewarded("test", "rewarded");

            Assert.IsFalse(Yes2SDK.Ads.IsAdShowing(), "teardown must not depend on a callback being supplied");
        }
    }
}
