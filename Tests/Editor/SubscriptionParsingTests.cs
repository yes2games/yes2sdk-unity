using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers typed subscription results: parsing of the platform payload
    /// shapes, parse failures reaching onError, and game code exceptions in
    /// onSuccess staying out of onError. Responses go through the same request
    /// registry and envelope parsing the Bridge uses.
    /// </summary>
    public class SubscriptionParsingTests
    {
        private const string Full =
            "{\"productId\":\"vip_monthly\",\"title\":\"VIP\",\"description\":\"All levels\"," +
            "\"price\":\"4.99 USD\",\"priceAmount\":4.99,\"priceCurrencyCode\":\"USD\"," +
            "\"billingPeriod\":\"monthly\",\"isActive\":true,\"trialEligible\":true," +
            "\"introOffer\":{\"priceAmount\":0.99,\"durationPeriods\":1}," +
            "\"retentionOffer\":{\"priceAmount\":1.99,\"durationPeriods\":3}," +
            "\"isSandbox\":true,\"signedRequest\":\"sig.abc\"}";

        private const string NullOffers =
            "{\"productId\":\"vip_yearly\",\"title\":\"VIP\",\"description\":\"\"," +
            "\"price\":\"39.99 USD\",\"priceAmount\":39.99,\"priceCurrencyCode\":\"USD\"," +
            "\"billingPeriod\":\"yearly\",\"isActive\":false,\"trialEligible\":false," +
            "\"introOffer\":null,\"retentionOffer\":null,\"signedRequest\":\"sig.def\"}";

        // isSandbox and signedRequest are optional in the platform type.
        private const string MissingOptional =
            "{\"productId\":\"vip_weekly\",\"title\":\"VIP\",\"description\":\"\"," +
            "\"price\":\"1.99 USD\",\"priceAmount\":1.99,\"priceCurrencyCode\":\"USD\"," +
            "\"billingPeriod\":\"weekly\",\"isActive\":false,\"trialEligible\":true," +
            "\"introOffer\":null,\"retentionOffer\":null}";

        private List<string> _calls;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            Yes2SDKIAP.ResetIapStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKIAP.ResetIapStateForTests();
        }

        // Registers a typed request the way the public API does and delivers
        // the bridge message for it.
        private void Deliver<T>(Yes2SDKIAP.Operation operation, Func<string, T> parse, string payload,
            Action<T> onSuccess, string context = "Yes2SDK.IAP.Test")
        {
            Action<Error> onError = e => _calls.Add("error:" + e.Code + ":" + e.Context);
            int id = Yes2SDKIAP.RegisterForTests(operation,
                Yes2SDKIAP.ParseThen(parse, onSuccess, onError, context), onError);
            Yes2SDKIAP.HandleSuccessMessage(operation, Yes2SDKIAP.EnvelopeForTests(id, payload));
        }

        [Test]
        public void Subscribed_WithFullSubscription_ParsesEveryField()
        {
            SubscribeResult result = null;
            Deliver(Yes2SDKIAP.Operation.Subscribe, SubscribeResult.Parse,
                "{\"status\":\"subscribed\",\"subscription\":" + Full + "}", r => result = r);

            Assert.IsEmpty(_calls);
            Assert.IsNotNull(result);
            Assert.AreEqual(SubscribeStatus.Subscribed, result.Status);
            Assert.IsTrue(result.IsSubscribed);
            var s = result.Subscription;
            Assert.IsNotNull(s);
            Assert.AreEqual("vip_monthly", s.ProductId);
            Assert.AreEqual("VIP", s.Title);
            Assert.AreEqual("All levels", s.Description);
            Assert.AreEqual("4.99 USD", s.Price);
            Assert.AreEqual(4.99, s.PriceAmount, 1e-9);
            Assert.AreEqual("USD", s.PriceCurrencyCode);
            Assert.AreEqual("monthly", s.BillingPeriod);
            Assert.IsTrue(s.IsActive);
            Assert.IsTrue(s.TrialEligible);
            Assert.IsNotNull(s.IntroOffer);
            Assert.AreEqual(0.99, s.IntroOffer.PriceAmount, 1e-9);
            Assert.AreEqual(1, s.IntroOffer.DurationPeriods);
            Assert.IsNotNull(s.RetentionOffer);
            Assert.AreEqual(1.99, s.RetentionOffer.PriceAmount, 1e-9);
            Assert.AreEqual(3, s.RetentionOffer.DurationPeriods);
            Assert.IsTrue(s.IsSandbox);
            Assert.AreEqual("sig.abc", s.SignedRequest);
        }

        [Test]
        public void Cancelled_WithoutSubscription_IsASuccessNotAnError()
        {
            SubscribeResult result = null;
            Deliver(Yes2SDKIAP.Operation.Subscribe, SubscribeResult.Parse,
                "{\"status\":\"cancelled\"}", r => result = r);

            Assert.IsEmpty(_calls);
            Assert.IsNotNull(result);
            Assert.AreEqual(SubscribeStatus.Cancelled, result.Status);
            Assert.IsFalse(result.IsSubscribed);
            Assert.IsNull(result.Subscription);
        }

        [Test]
        public void UnknownStatus_GoesToOnError()
        {
            Deliver(Yes2SDKIAP.Operation.Subscribe, SubscribeResult.Parse,
                "{\"status\":\"pending\"}", _ => _calls.Add("success"), "Yes2SDK.IAP.SubscribeAsync");

            Assert.AreEqual(new[] { "error:Unknown:Yes2SDK.IAP.SubscribeAsync" }, _calls);
        }

        [Test]
        public void SubscribedWithoutSubscription_GoesToOnError()
        {
            Deliver(Yes2SDKIAP.Operation.Subscribe, SubscribeResult.Parse,
                "{\"status\":\"subscribed\"}", _ => _calls.Add("success"), "Yes2SDK.IAP.SubscribeAsync");

            Assert.AreEqual(new[] { "error:Unknown:Yes2SDK.IAP.SubscribeAsync" }, _calls);
        }

        [Test]
        public void NullOffers_ParseAsNull()
        {
            Subscription s = null;
            Deliver(Yes2SDKIAP.Operation.ClaimRetentionOffer, Subscription.Parse, NullOffers, r => s = r);

            Assert.IsNotNull(s);
            Assert.AreEqual("yearly", s.BillingPeriod);
            Assert.IsFalse(s.IsActive);
            Assert.IsNull(s.IntroOffer);
            Assert.IsNull(s.RetentionOffer);
            Assert.AreEqual("sig.def", s.SignedRequest);
        }

        [Test]
        public void MissingOptionalFields_DefaultSandboxFalseAndNoSignedRequest()
        {
            Subscription s = null;
            Deliver(Yes2SDKIAP.Operation.ClaimRetentionOffer, Subscription.Parse, MissingOptional, r => s = r);

            Assert.IsNotNull(s);
            Assert.AreEqual("vip_weekly", s.ProductId);
            Assert.IsFalse(s.IsSandbox);
            Assert.IsNull(s.SignedRequest);
        }

        [Test]
        public void SubscriptionList_ParsesEveryEntry()
        {
            List<Subscription> list = null;
            Deliver(Yes2SDKIAP.Operation.GetSubscriptions, Subscription.ParseList,
                "[" + Full + "," + NullOffers + "]", r => list = r);

            Assert.IsNotNull(list);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("vip_monthly", list[0].ProductId);
            Assert.AreEqual("vip_yearly", list[1].ProductId);
        }

        [Test]
        public void EmptySubscriptionList_ParsesAsEmpty()
        {
            List<Subscription> list = null;
            Deliver(Yes2SDKIAP.Operation.GetSubscriptions, Subscription.ParseList, "[]", r => list = r);

            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }

        // Operation is internal, so cases name it and parse it back (see IAPCallbackTests).
        [TestCase(nameof(Yes2SDKIAP.Operation.GetSubscriptions), "[{oops")]
        [TestCase(nameof(Yes2SDKIAP.Operation.Subscribe), "{not json")]
        [TestCase(nameof(Yes2SDKIAP.Operation.ClaimRetentionOffer), "")]
        [TestCase(nameof(Yes2SDKIAP.Operation.ClaimRetentionOffer), "null")]
        [TestCase(nameof(Yes2SDKIAP.Operation.CancelSubscription), "maybe")]
        public void MalformedPayload_GoesToOnError(string operationName, string payload)
        {
            var operation = (Yes2SDKIAP.Operation)Enum.Parse(typeof(Yes2SDKIAP.Operation), operationName);
            switch (operation)
            {
                case Yes2SDKIAP.Operation.GetSubscriptions:
                    Deliver(operation, Subscription.ParseList, payload, _ => _calls.Add("success"));
                    break;
                case Yes2SDKIAP.Operation.Subscribe:
                    Deliver(operation, SubscribeResult.Parse, payload, _ => _calls.Add("success"));
                    break;
                case Yes2SDKIAP.Operation.CancelSubscription:
                    Deliver(operation, Yes2SDKIAP.ParseConfirmation, payload, _ => _calls.Add("success"));
                    break;
                default:
                    Deliver(operation, Subscription.Parse, payload, _ => _calls.Add("success"));
                    break;
            }

            Assert.AreEqual(new[] { "error:Unknown:Yes2SDK.IAP.Test" }, _calls);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }

        [Test]
        public void ThrowingOnSuccess_DoesNotCallOnError()
        {
            // The exception must reach the Bridge's logger, not be turned into
            // a parse failure.
            Assert.Throws<InvalidOperationException>(() =>
                Deliver(Yes2SDKIAP.Operation.Subscribe, SubscribeResult.Parse,
                    "{\"status\":\"cancelled\"}",
                    _ => throw new InvalidOperationException("game code failed")));

            Assert.IsEmpty(_calls);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }

        // Date-like strings must reach string fields unchanged (no DateTime
        // round trip that reformats them).
        private const string DateLike = "2026-10-06T12:34:56Z";

        private static string DateLikeSubscription =>
            "{\"productId\":\"vip_monthly\",\"title\":\"" + DateLike + "\",\"description\":\"" + DateLike + "\"," +
            "\"price\":\"4.99 USD\",\"priceAmount\":4.99,\"priceCurrencyCode\":\"USD\"," +
            "\"billingPeriod\":\"monthly\",\"isActive\":true,\"trialEligible\":false," +
            "\"introOffer\":null,\"retentionOffer\":null,\"signedRequest\":\"" + DateLike + "\"}";

        [Test]
        public void DateLikeStrings_RoundTripUnchanged_InEveryParser()
        {
            var fromResult = SubscribeResult.Parse(
                "{\"status\":\"subscribed\",\"subscription\":" + DateLikeSubscription + "}").Subscription;
            var fromSingle = Subscription.Parse(DateLikeSubscription);
            var fromList = Subscription.ParseList("[" + DateLikeSubscription + "]")[0];

            foreach (var s in new[] { fromResult, fromSingle, fromList })
            {
                Assert.AreEqual(DateLike, s.Title);
                Assert.AreEqual(DateLike, s.Description);
                Assert.AreEqual(DateLike, s.SignedRequest);
            }
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        public void CancelSubscription_PayloadIsTheConfirmation(string payload, bool expected)
        {
            bool? confirmed = null;
            Deliver(Yes2SDKIAP.Operation.CancelSubscription, Yes2SDKIAP.ParseConfirmation, payload,
                r => confirmed = r);

            Assert.IsEmpty(_calls);
            Assert.AreEqual(expected, confirmed);
        }

        // --- Public API on the Editor path (headless: mock off) ---

        [Test]
        public void PublicApi_WithoutAMockOrPlatform_ReportsUnsupportedOnce()
        {
            var codes = new List<string>();
            int successes = 0;

            Assert.IsFalse(Yes2SDK.IAP.IsSubscriptionSupported());
            Yes2SDK.IAP.GetSubscriptionsAsync(_ => successes++, e => codes.Add(e.Code));
            Yes2SDK.IAP.SubscribeAsync("vip", _ => successes++, e => codes.Add(e.Code));
            Yes2SDK.IAP.CancelSubscriptionAsync("vip", _ => successes++, e => codes.Add(e.Code));
            Yes2SDK.IAP.ClaimRetentionOfferAsync("vip", _ => successes++, e => codes.Add(e.Code));

            Assert.AreEqual(0, successes);
            Assert.AreEqual(new[] { "FeatureNotSupported", "FeatureNotSupported", "FeatureNotSupported", "FeatureNotSupported" }, codes);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }

        [TestCase(null)]
        [TestCase("")]
        public void PublicApi_EmptyProductId_FailsWithInvalidParams(string productId)
        {
            var codes = new List<string>();

            Yes2SDK.IAP.SubscribeAsync(productId, _ => codes.Add("success"), e => codes.Add(e.Code + ":" + e.Context));
            Yes2SDK.IAP.CancelSubscriptionAsync(productId, _ => codes.Add("success"), e => codes.Add(e.Code + ":" + e.Context));
            Yes2SDK.IAP.ClaimRetentionOfferAsync(productId, _ => codes.Add("success"), e => codes.Add(e.Code + ":" + e.Context));

            Assert.AreEqual(new[]
            {
                "InvalidParams:Yes2SDK.IAP.SubscribeAsync",
                "InvalidParams:Yes2SDK.IAP.CancelSubscriptionAsync",
                "InvalidParams:Yes2SDK.IAP.ClaimRetentionOfferAsync"
            }, codes);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }

        [Test]
        public void TaskOverload_FaultsWithTheError()
        {
            var task = Yes2SDK.IAP.SubscribeAsync("", System.Threading.CancellationToken.None);

            Assert.IsTrue(task.IsFaulted);
            var ex = task.Exception.InnerException as Yes2SDKException;
            Assert.IsNotNull(ex);
            Assert.AreEqual("InvalidParams", ex.SdkError.Code);
            Assert.AreEqual(ErrorCode.InvalidParams, ex.ErrorCode);
        }
    }
}
