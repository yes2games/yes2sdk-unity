using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the referrals module: each response reaches only the call that
    /// made it, a completed call is cleaned up before game code runs, typed
    /// results parse from the bridge payload, share options serialize to the
    /// platform shape, and the Editor mock outcomes.
    ///
    /// Requests are registered without being sent and the bridge messages are
    /// delivered by hand, through the same envelope parsing the Bridge uses.
    /// </summary>
    public class ReferralsCallbackTests
    {
        private static readonly string[] AllOperations =
        {
            nameof(Yes2SDKReferrals.Operation.Share),
            nameof(Yes2SDKReferrals.Operation.List)
        };

        private static Yes2SDKReferrals.Operation Op(string name)
        {
            return (Yes2SDKReferrals.Operation)Enum.Parse(typeof(Yes2SDKReferrals.Operation), name);
        }

        private List<string> _calls;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            Yes2SDKReferrals.ResetReferralsStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKReferrals.ResetReferralsStateForTests();
        }

        private int Register(Yes2SDKReferrals.Operation operation, string label)
        {
            return Yes2SDKReferrals.RegisterForTests(operation,
                data => _calls.Add($"{label}:success:{data}"),
                error => _calls.Add($"{label}:error:{error.Code}"));
        }

        private static void DeliverSuccess(Yes2SDKReferrals.Operation operation, int requestId, string payload)
        {
            Yes2SDKReferrals.HandleSuccessMessage(operation, Yes2SDKReferrals.EnvelopeForTests(requestId, payload));
        }

        private static void DeliverError(Yes2SDKReferrals.Operation operation, int requestId, string code)
        {
            string json = "{\"code\":\"" + code + "\",\"message\":\"m\",\"context\":\"c\"}";
            Yes2SDKReferrals.HandleErrorMessage(operation, Yes2SDKReferrals.EnvelopeForTests(requestId, json), Bridge.ParseError);
        }

        // --- Routing: responses reach only their own request ---

        [Test]
        public void DelayedResponse_CompletesOnlyItsOwnRequest([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int a = Register(op, "A");
            int b = Register(op, "B");

            DeliverSuccess(op, a, "a");
            DeliverSuccess(op, b, "b");

            Assert.AreEqual(new[] { "A:success:a", "B:success:b" }, _calls);
            Assert.AreEqual(0, Yes2SDKReferrals.PendingCountForTests);
        }

        [Test]
        public void ResponsesInReverseOrder_StillReachTheirOwnRequest([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int a = Register(op, "A");
            int b = Register(op, "B");

            DeliverSuccess(op, b, "b");
            DeliverSuccess(op, a, "a");

            Assert.AreEqual(new[] { "B:success:b", "A:success:a" }, _calls);
        }

        [Test]
        public void DelayedError_DoesNotCompleteOrClearTheRetry([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int a = Register(op, "A");
            int b = Register(op, "B");

            DeliverError(op, a, "Timeout");
            Assert.AreEqual(new[] { "A:error:Timeout" }, _calls);
            Assert.AreEqual(1, Yes2SDKReferrals.PendingCountForTests, "B must still be pending after A's error");

            DeliverSuccess(op, b, "b");
            Assert.AreEqual(new[] { "A:error:Timeout", "B:success:b" }, _calls);
        }

        [Test]
        public void DuplicateResponse_IsDropped([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int a = Register(op, "A");

            DeliverSuccess(op, a, "a");
            DeliverSuccess(op, a, "again");
            DeliverError(op, a, "Late");

            Assert.AreEqual(new[] { "A:success:a" }, _calls);
        }

        [Test]
        public void ResponseOnAnotherOperationsChannel_IsDropped()
        {
            int list = Register(Yes2SDKReferrals.Operation.List, "list");

            DeliverSuccess(Yes2SDKReferrals.Operation.Share, list, "wrong");
            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKReferrals.PendingCountForTests, "a mismatched response must not remove the request");

            DeliverSuccess(Yes2SDKReferrals.Operation.List, list, "{}");
            Assert.AreEqual(new[] { "list:success:{}" }, _calls);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("|{}")]
        [TestCase("abc|{}")]
        [TestCase("-1|{}")]
        public void MessageWithoutARequestId_IsDropped(string message)
        {
            Register(Yes2SDKReferrals.Operation.Share, "A");

            Yes2SDKReferrals.HandleSuccessMessage(Yes2SDKReferrals.Operation.Share, message);

            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKReferrals.PendingCountForTests);
        }

        [Test]
        public void PayloadContainingTheSeparator_IsDeliveredWhole()
        {
            int a = Register(Yes2SDKReferrals.Operation.List, "A");

            DeliverSuccess(Yes2SDKReferrals.Operation.List, a, "{\"signedRequest\":\"x|y\"}");

            Assert.AreEqual(new[] { "A:success:{\"signedRequest\":\"x|y\"}" }, _calls);
        }

        // --- Cleanup runs before game code ---

        [Test]
        public void StartingTheNextRequestFromASuccessCallback_KeepsItsCallbacks([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = 0;
            int a = Yes2SDKReferrals.RegisterForTests(op,
                _ =>
                {
                    _calls.Add("A:success");
                    b = Register(op, "B");
                },
                _ => _calls.Add("A:error"));

            DeliverSuccess(op, a, "a");
            DeliverSuccess(op, b, "b");

            Assert.AreEqual(new[] { "A:success", "B:success:b" }, _calls);
            Assert.AreEqual(0, Yes2SDKReferrals.PendingCountForTests);
        }

        [Test]
        public void StartingTheNextRequestFromAnErrorCallback_KeepsItsCallbacks([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = 0;
            int a = Yes2SDKReferrals.RegisterForTests(op,
                _ => _calls.Add("A:success"),
                _ =>
                {
                    _calls.Add("A:error");
                    b = Register(op, "B");
                });

            DeliverError(op, a, "Failed");
            DeliverError(op, b, "Failed");

            Assert.AreEqual(new[] { "A:error", "B:error:Failed" }, _calls);
        }

        [Test]
        public void ThrowingCallback_StillCompletesItsRequest([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = Register(op, "B");
            int a = Yes2SDKReferrals.RegisterForTests(op,
                _ => throw new InvalidOperationException("game code failed"),
                _ => { });

            // The Bridge catches and logs this; here it reaches the test.
            Assert.Throws<InvalidOperationException>(() => DeliverSuccess(op, a, "a"));

            Assert.AreEqual(1, Yes2SDKReferrals.PendingCountForTests, "A is removed even though its callback threw");
            DeliverSuccess(op, a, "again");
            DeliverSuccess(op, b, "b");
            Assert.AreEqual(new[] { "B:success:b" }, _calls);
        }

        [Test]
        public void ErrorPayload_IsParsedIntoTheErrorCode()
        {
            Error received = default;
            int a = Yes2SDKReferrals.RegisterForTests(Yes2SDKReferrals.Operation.Share, _ => { }, e => received = e);

            DeliverError(Yes2SDKReferrals.Operation.Share, a, "FEATURE_NOT_SUPPORTED");

            Assert.AreEqual("FEATURE_NOT_SUPPORTED", received.Code);
            Assert.AreEqual(ErrorCode.FeatureNotSupported, received.ErrorCode);
        }

        // --- Typed results from the bridge payload ---

        [Test]
        public void ShareResult_IsParsedFromThePayload()
        {
            var results = new List<bool>();
            int a = Yes2SDKReferrals.RegisterShareForTests(r => results.Add(r.Canceled), e => Assert.Fail(e.Message));
            int b = Yes2SDKReferrals.RegisterShareForTests(r => results.Add(r.Canceled), e => Assert.Fail(e.Message));

            DeliverSuccess(Yes2SDKReferrals.Operation.Share, a, "{\"canceled\":true}");
            DeliverSuccess(Yes2SDKReferrals.Operation.Share, b, "{\"canceled\":false}");

            Assert.AreEqual(new[] { true, false }, results);
        }

        [Test]
        public void List_WithNestedReferralsMap_IsParsed()
        {
            ReferralList list = null;
            int a = Yes2SDKReferrals.RegisterListForTests(l => list = l, e => Assert.Fail(e.Message));

            DeliverSuccess(Yes2SDKReferrals.Operation.List, a,
                "{\"referrals\":{" +
                "\"party_v1\":[{\"playerId\":\"p1\",\"joinedAt\":\"2026-10-06T08:00:00.000Z\"},{\"playerId\":\"p2\",\"joinedAt\":\"2026-10-06T09:30:00.000Z\"}]," +
                "\"skin_v2\":[]}," +
                "\"signedRequest\":\"sig.payload\"}");

            Assert.IsNotNull(list);
            Assert.AreEqual("sig.payload", list.SignedRequest);
            Assert.AreEqual(2, list.Referrals.Count);
            Assert.AreEqual(2, list.Referrals["party_v1"].Count);
            Assert.AreEqual("p1", list.Referrals["party_v1"][0].PlayerId);
            // ISO timestamps stay exactly as the platform sent them (no date conversion).
            Assert.AreEqual("2026-10-06T08:00:00.000Z", list.Referrals["party_v1"][0].JoinedAt);
            Assert.AreEqual("p2", list.Referrals["party_v1"][1].PlayerId);
            Assert.IsEmpty(list.Referrals["skin_v2"]);
        }

        [Test]
        public void List_WithEmptyReferralsMap_IsParsed()
        {
            ReferralList list = null;
            int a = Yes2SDKReferrals.RegisterListForTests(l => list = l, e => Assert.Fail(e.Message));

            DeliverSuccess(Yes2SDKReferrals.Operation.List, a, "{\"referrals\":{},\"signedRequest\":\"s\"}");

            Assert.IsNotNull(list);
            Assert.IsNotNull(list.Referrals);
            Assert.IsEmpty(list.Referrals);
            Assert.AreEqual("s", list.SignedRequest);
        }

        [TestCase("{}")]
        [TestCase("{\"referrals\":null}")]
        public void List_WithoutReferrals_GivesAnEmptyMap(string payload)
        {
            ReferralList list = null;
            int a = Yes2SDKReferrals.RegisterListForTests(l => list = l, e => Assert.Fail(e.Message));

            DeliverSuccess(Yes2SDKReferrals.Operation.List, a, payload);

            Assert.IsNotNull(list);
            Assert.IsNotNull(list.Referrals);
            Assert.IsEmpty(list.Referrals);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("null")]
        public void UnparseablePayload_ReachesOnError(string payload)
        {
            Error shareError = default, listError = default;
            int share = Yes2SDKReferrals.RegisterShareForTests(_ => Assert.Fail("share success"), e => shareError = e);
            int list = Yes2SDKReferrals.RegisterListForTests(_ => Assert.Fail("list success"), e => listError = e);

            DeliverSuccess(Yes2SDKReferrals.Operation.Share, share, payload);
            DeliverSuccess(Yes2SDKReferrals.Operation.List, list, payload);

            Assert.AreEqual(ErrorCode.PlatformError, shareError.ErrorCode);
            Assert.AreEqual(ErrorCode.PlatformError, listError.ErrorCode);
            Assert.AreEqual(0, Yes2SDKReferrals.PendingCountForTests);
        }

        // --- Share options serialization ---

        [Test]
        public void ShareOptions_WithOnlyAReference_SerializeWithoutUnsetFields()
        {
            var json = JObject.Parse(new ReferralShareOptions { Reference = "party_v1" }.ToJson());

            Assert.AreEqual(1, json.Count, json.ToString());
            Assert.AreEqual("party_v1", (string)json["reference"]);
        }

        [Test]
        public void ShareOptions_WithEveryField_UseThePlatformKeyNames()
        {
            var options = new ReferralShareOptions("party_v1")
            {
                Data = new Dictionary<string, object> { ["room"] = "abc", ["level"] = 3 },
                Title = "Join me",
                Text = "Play with me",
                ImageDataUrl = "data:image/png;base64,AAAA"
            };

            var json = JObject.Parse(options.ToJson());

            Assert.AreEqual("party_v1", (string)json["reference"]);
            Assert.AreEqual("abc", (string)json["data"]["room"]);
            Assert.AreEqual(3, (int)json["data"]["level"]);
            Assert.AreEqual("Join me", (string)json["title"]);
            Assert.AreEqual("Play with me", (string)json["text"]);
            Assert.AreEqual("data:image/png;base64,AAAA", (string)json["image"]);
            Assert.IsNull(json["imageDataUrl"], "the image goes under the platform key 'image'");
            Assert.IsNull(json["ImageDataUrl"]);
            Assert.AreEqual(5, json.Count, json.ToString());
        }

        // --- Synchronous InvalidParams ---

        private static IEnumerable<ReferralShareOptions> InvalidOptions()
        {
            yield return null;
            yield return new ReferralShareOptions();
            yield return new ReferralShareOptions { Reference = "" };
            yield return new ReferralShareOptions { Reference = "   " };
        }

        [Test]
        public void ShareAsync_WithoutAReference_FailsSynchronouslyWithInvalidParams(
            [ValueSource(nameof(InvalidOptions))] ReferralShareOptions options)
        {
            Error received = default;
            int errors = 0;

            Yes2SDK.Referrals.ShareAsync(options, _ => Assert.Fail("success"), e => { errors++; received = e; });

            Assert.AreEqual(1, errors);
            Assert.AreEqual(ErrorCode.InvalidParams, received.ErrorCode);
            Assert.AreEqual(0, Yes2SDKReferrals.PendingCountForTests);
        }

        // --- Public API on the Editor path (no popups headless, so the mock is off) ---

        [Test]
        public void PublicApi_WithoutTheMock_ReportsFeatureNotSupportedOnce()
        {
            var codes = new List<ErrorCode>();
            int successes = 0;

            Yes2SDK.Referrals.ShareAsync(new ReferralShareOptions("party_v1"), _ => successes++, e => codes.Add(e.ErrorCode));
            Yes2SDK.Referrals.ListAsync(_ => successes++, e => codes.Add(e.ErrorCode));

            Assert.AreEqual(0, successes);
            Assert.AreEqual(new[] { ErrorCode.FeatureNotSupported, ErrorCode.FeatureNotSupported }, codes);
            Assert.AreEqual(0, Yes2SDKReferrals.PendingCountForTests);
            Assert.IsFalse(Yes2SDK.Referrals.IsSupported());
        }

        [Test]
        public void TaskOverloads_FaultWithTheError()
        {
            var share = Yes2SDK.Referrals.ShareAsync(new ReferralShareOptions("party_v1"), CancellationToken.None);
            var list = Yes2SDK.Referrals.ListAsync(CancellationToken.None);
            var invalid = Yes2SDK.Referrals.ShareAsync(null, CancellationToken.None);

            Assert.IsTrue(share.IsFaulted);
            Assert.IsTrue(list.IsFaulted);
            Assert.IsTrue(invalid.IsFaulted);
            var ex = (Yes2SDKException)invalid.Exception.InnerException;
            Assert.AreEqual(ErrorCode.InvalidParams, ex.ErrorCode);
        }

        // --- Editor mock outcomes (called directly: CanShowPopups is false in batch mode) ---

        [Test]
        public void MockShare_Shared_ResolvesNotCanceledAndIsListed()
        {
            ReferralShareResult result = null;
            Yes2SDKReferrals.MockShareForTests(new ReferralShareOptions("party_v1"),
                Yes2SDKEditorMock.ShareOutcome.Shared, r => result = r, e => Assert.Fail(e.Message));

            ReferralList list = null;
            Yes2SDKReferrals.MockListForTests(2, l => list = l, e => Assert.Fail(e.Message));

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Canceled);
            Assert.AreEqual("mock-signed-request", list.SignedRequest);
            Assert.AreEqual(new[] { "party_v1" }, new List<string>(list.Referrals.Keys));
            var conversions = list.Referrals["party_v1"];
            Assert.AreEqual(2, conversions.Count);
            Assert.AreEqual("mock-player-1", conversions[0].PlayerId);
            Assert.AreEqual("mock-player-2", conversions[1].PlayerId);
            Assert.IsTrue(DateTime.TryParse(conversions[0].JoinedAt, out _), conversions[0].JoinedAt);
            StringAssert.EndsWith("Z", conversions[0].JoinedAt);
            Assert.AreEqual(0, Yes2SDKReferrals.PendingCountForTests);
        }

        [Test]
        public void MockShare_SameReferenceTwice_IsListedOnce()
        {
            var options = new ReferralShareOptions("party_v1");
            Yes2SDKReferrals.MockShareForTests(options, Yes2SDKEditorMock.ShareOutcome.Shared, _ => { }, _ => { });
            Yes2SDKReferrals.MockShareForTests(options, Yes2SDKEditorMock.ShareOutcome.Shared, _ => { }, _ => { });
            Yes2SDKReferrals.MockShareForTests(new ReferralShareOptions("skin_v2"), Yes2SDKEditorMock.ShareOutcome.Shared, _ => { }, _ => { });

            ReferralList list = null;
            Yes2SDKReferrals.MockListForTests(1, l => list = l, _ => { });

            Assert.AreEqual(new[] { "party_v1", "skin_v2" }, new List<string>(list.Referrals.Keys));
        }

        [Test]
        public void MockShare_Cancelled_ResolvesCanceledAndIsNotListed()
        {
            ReferralShareResult result = null;
            Yes2SDKReferrals.MockShareForTests(new ReferralShareOptions("party_v1"),
                Yes2SDKEditorMock.ShareOutcome.Cancelled, r => result = r, e => Assert.Fail(e.Message));

            ReferralList list = null;
            Yes2SDKReferrals.MockListForTests(1, l => list = l, _ => { });

            Assert.IsTrue(result.Canceled);
            Assert.IsEmpty(list.Referrals);
        }

        [Test]
        public void MockShare_Error_FailsWithPlatformError()
        {
            Error received = default;
            Yes2SDKReferrals.MockShareForTests(new ReferralShareOptions("party_v1"),
                Yes2SDKEditorMock.ShareOutcome.Error, _ => Assert.Fail("success"), e => received = e);

            ReferralList list = null;
            Yes2SDKReferrals.MockListForTests(1, l => list = l, _ => { });

            Assert.AreEqual(ErrorCode.PlatformError, received.ErrorCode);
            Assert.IsEmpty(list.Referrals);
            Assert.AreEqual(0, Yes2SDKReferrals.PendingCountForTests);
        }

        [Test]
        public void MockList_WithNoConversions_OmitsTheReference()
        {
            Yes2SDKReferrals.MockShareForTests(new ReferralShareOptions("party_v1"),
                Yes2SDKEditorMock.ShareOutcome.Shared, _ => { }, _ => { });

            ReferralList list = null;
            Yes2SDKReferrals.MockListForTests(0, l => list = l, _ => { });

            Assert.IsEmpty(list.Referrals);
            Assert.AreEqual("mock-signed-request", list.SignedRequest);
        }

        [Test]
        public void ResetState_ForgetsMockShares()
        {
            Yes2SDKReferrals.MockShareForTests(new ReferralShareOptions("party_v1"),
                Yes2SDKEditorMock.ShareOutcome.Shared, _ => { }, _ => { });
            Yes2SDKReferrals.ResetReferralsStateForTests();

            ReferralList list = null;
            Yes2SDKReferrals.MockListForTests(1, l => list = l, _ => { });

            Assert.IsEmpty(list.Referrals);
        }
    }
}
