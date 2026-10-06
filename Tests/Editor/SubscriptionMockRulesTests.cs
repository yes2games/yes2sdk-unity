using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the Editor mock subscription rules: the per-call decision
    /// (guest, unknown product, held, not held) and the offer visibility of
    /// the mock subscription. The mock mirrors the platform error codes so
    /// games can exercise their real error paths in the Editor.
    /// </summary>
    public class SubscriptionMockRulesTests
    {
        private const string Known = Yes2SDKMockIAP.MockSubscriptionId;
        private const string Context = "Yes2SDK.IAP.Test";

        [SetUp]
        public void SetUp()
        {
            Yes2SDKMockIAP.ResetState();
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKMockIAP.ResetState();
        }

        private static string Decide(Yes2SDKMockIAP.SubscriptionCall call, string productId, bool registered, bool held)
        {
            if (!Yes2SDKMockIAP.TryRejectSubscription(call, productId, registered, held, Context, out var error))
                return "proceed";
            Assert.AreEqual(Context, error.Context);
            Assert.IsFalse(string.IsNullOrEmpty(error.Message));
            return error.Code;
        }

        // --- Subscribe ---

        [TestCase(true, false, "proceed")]
        [TestCase(true, true, "IAP_ALREADY_PURCHASED")]
        [TestCase(false, false, "PLAYER_NOT_AUTHENTICATED")]
        [TestCase(false, true, "PLAYER_NOT_AUTHENTICATED")]
        public void Subscribe_KnownProduct(bool registered, bool held, string expected)
        {
            Assert.AreEqual(expected, Decide(Yes2SDKMockIAP.SubscriptionCall.Subscribe, Known, registered, held));
        }

        [Test]
        public void Subscribe_UnknownProduct_IsNotAvailable()
        {
            Assert.AreEqual("IAP_NOT_AVAILABLE", Decide(Yes2SDKMockIAP.SubscriptionCall.Subscribe, "other", true, false));
        }

        // --- Cancel ---

        [TestCase(true, true, "proceed")]
        [TestCase(true, false, "INVALID_OPERATION")]
        [TestCase(false, true, "PLAYER_NOT_AUTHENTICATED")]
        [TestCase(false, false, "PLAYER_NOT_AUTHENTICATED")]
        public void Cancel_KnownProduct(bool registered, bool held, string expected)
        {
            Assert.AreEqual(expected, Decide(Yes2SDKMockIAP.SubscriptionCall.CancelSubscription, Known, registered, held));
        }

        [Test]
        public void Cancel_UnknownProduct_IsNotAvailable()
        {
            Assert.AreEqual("IAP_NOT_AVAILABLE", Decide(Yes2SDKMockIAP.SubscriptionCall.CancelSubscription, "other", true, true));
        }

        // --- Claim retention offer ---

        [TestCase(true, true, "proceed")]
        [TestCase(true, false, "INVALID_OPERATION")]
        [TestCase(false, true, "PLAYER_NOT_AUTHENTICATED")]
        [TestCase(false, false, "PLAYER_NOT_AUTHENTICATED")]
        public void Claim_KnownProduct(bool registered, bool held, string expected)
        {
            Assert.AreEqual(expected, Decide(Yes2SDKMockIAP.SubscriptionCall.ClaimRetentionOffer, Known, registered, held));
        }

        [Test]
        public void Claim_UnknownProduct_IsNotEligible()
        {
            // The platform has no "not found" for a claim: an unknown product is
            // simply not eligible.
            Assert.AreEqual("INVALID_OPERATION", Decide(Yes2SDKMockIAP.SubscriptionCall.ClaimRetentionOffer, "other", true, true));
        }

        [Test]
        public void Claim_Repeated_SucceedsWithTheSameResult()
        {
            Yes2SDKMockIAP.Subscribe();

            string first = Yes2SDKMockIAP.ClaimRetentionOffer();
            Assert.AreEqual("proceed", Decide(Yes2SDKMockIAP.SubscriptionCall.ClaimRetentionOffer, Known, true,
                Yes2SDKMockIAP.SubscriptionActive));
            string second = Yes2SDKMockIAP.ClaimRetentionOffer();

            Assert.AreEqual(first, second);
            Assert.IsNull(Yes2SDKMockIAP.CurrentSubscription().RetentionOffer);
        }

        // --- Cancel keeps access to the end of the period ---

        [Test]
        public void ConfirmedCancel_KeepsTheSubscriptionActive()
        {
            Yes2SDKMockIAP.Subscribe();

            Yes2SDKMockIAP.CancelSubscription();

            Assert.IsTrue(Yes2SDKMockIAP.SubscriptionActive);
            Assert.IsTrue(Yes2SDKMockIAP.SubscriptionCancelled);
            Assert.IsTrue(Yes2SDKMockIAP.CurrentSubscription().IsActive);
        }

        // --- Offer visibility ---

        [Test]
        public void NotSubscribed_HasIntroOfferButNoRetentionOffer()
        {
            var s = Yes2SDKMockIAP.CurrentSubscription();

            Assert.IsFalse(s.IsActive);
            Assert.IsTrue(s.TrialEligible);
            Assert.IsNotNull(s.IntroOffer);
            Assert.IsNull(s.RetentionOffer);
        }

        [Test]
        public void Subscribed_HasRetentionOfferUntilClaimed()
        {
            Yes2SDKMockIAP.Subscribe();
            var before = Yes2SDKMockIAP.CurrentSubscription();
            Yes2SDKMockIAP.ClaimRetentionOffer();
            var after = Yes2SDKMockIAP.CurrentSubscription();

            Assert.IsTrue(before.IsActive);
            Assert.IsNull(before.IntroOffer);
            Assert.IsNotNull(before.RetentionOffer);
            Assert.IsNull(after.RetentionOffer);
        }

        [Test]
        public void SubscriptionJson_OmitsSandboxFlag()
        {
            // The mock carries offers, which a sandbox player never gets, so it
            // reports a non-sandbox subscription: the flag is omitted, as the
            // platform omits it when false.
            StringAssert.DoesNotContain("isSandbox", Yes2SDKMockIAP.SubscriptionsJson);
            StringAssert.DoesNotContain("isSandbox", Yes2SDKMockIAP.Subscribe());
        }
    }
}
