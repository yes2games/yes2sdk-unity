using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers Context.ShareAsync and ShareImageAsync: each response reaches only the call that made
    /// it, the share payload serializes to the platform shape, a raw base64
    /// image gets the PNG data URL prefix, invalid input fails synchronously
    /// without leaving a pending request, the 2.9.0 call shapes still
    /// compiling, the Editor mock, and the other
    /// Context methods staying unsupported.
    ///
    /// Requests are registered without being sent and the bridge messages are
    /// delivered by hand, through the same envelope parsing the Bridge uses.
    /// </summary>
    public class ContextShareTests
    {
        private List<string> _calls;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            Yes2SDKContext.ResetContextStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKContext.ResetContextStateForTests();
        }

        private int Register(string label)
        {
            return Yes2SDKContext.RegisterShareForTests(
                () => _calls.Add($"{label}:success"),
                error => _calls.Add($"{label}:error:{error.Code}"));
        }

        private static void DeliverSuccess(int requestId, string payload = "")
        {
            Yes2SDKContext.HandleSuccessMessage(Yes2SDKContext.Operation.Share, Yes2SDKContext.EnvelopeForTests(requestId, payload));
        }

        private static void DeliverError(int requestId, string code)
        {
            string json = "{\"code\":\"" + code + "\",\"message\":\"m\",\"context\":\"c\"}";
            Yes2SDKContext.HandleErrorMessage(Yes2SDKContext.Operation.Share, Yes2SDKContext.EnvelopeForTests(requestId, json), Bridge.ParseError);
        }

        // --- Routing: responses reach only their own request ---

        [Test]
        public void DelayedResponses_CompleteOnlyTheirOwnRequest()
        {
            int a = Register("A");
            int b = Register("B");

            DeliverSuccess(b);
            DeliverSuccess(a);

            Assert.AreEqual(new[] { "B:success", "A:success" }, _calls);
            Assert.AreEqual(0, Yes2SDKContext.PendingCountForTests);
        }

        [Test]
        public void DelayedError_DoesNotCompleteOrClearTheRetry()
        {
            int a = Register("A");
            int b = Register("B");

            DeliverError(a, "Timeout");
            Assert.AreEqual(new[] { "A:error:Timeout" }, _calls);
            Assert.AreEqual(1, Yes2SDKContext.PendingCountForTests, "B must still be pending after A's error");

            DeliverSuccess(b);
            Assert.AreEqual(new[] { "A:error:Timeout", "B:success" }, _calls);
        }

        [Test]
        public void DuplicateResponse_IsDropped()
        {
            int a = Register("A");

            DeliverSuccess(a);
            DeliverSuccess(a);
            DeliverError(a, "Late");

            Assert.AreEqual(new[] { "A:success" }, _calls);
        }

        [Test]
        public void StaleResponseForAnUnknownRequest_IsDropped()
        {
            int a = Register("A");

            DeliverSuccess(a + 100);
            DeliverError(a + 101, "Late");

            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKContext.PendingCountForTests);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("|")]
        [TestCase("abc|")]
        [TestCase("-1|")]
        public void MessageWithoutARequestId_IsDropped(string message)
        {
            Register("A");

            Yes2SDKContext.HandleSuccessMessage(Yes2SDKContext.Operation.Share, message);

            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKContext.PendingCountForTests);
        }

        [Test]
        public void StartingTheNextShareFromASuccessCallback_KeepsItsCallbacks()
        {
            int b = 0;
            int a = Yes2SDKContext.RegisterShareForTests(
                () =>
                {
                    _calls.Add("A:success");
                    b = Register("B");
                },
                _ => _calls.Add("A:error"));

            DeliverSuccess(a);
            DeliverSuccess(b);

            Assert.AreEqual(new[] { "A:success", "B:success" }, _calls);
            Assert.AreEqual(0, Yes2SDKContext.PendingCountForTests);
        }

        [Test]
        public void ErrorPayload_IsParsedIntoTheErrorCode()
        {
            Error received = default;
            int a = Yes2SDKContext.RegisterShareForTests(() => { }, e => received = e);

            DeliverError(a, "FEATURE_NOT_SUPPORTED");

            Assert.AreEqual(ErrorCode.FeatureNotSupported, received.ErrorCode);
        }

        // --- Payload serialization ---

        [Test]
        public void Payload_WithEveryField_UsesThePlatformShape()
        {
            var data = new Dictionary<string, object>
            {
                ["coupon"] = "SPRING25",
                ["room"] = new Dictionary<string, object> { ["id"] = "r1" }
            };

            bool ok = Yes2SDKContext.TrySerializeSharePayload("Look at this", "data:image/png;base64,AAAA", data,
                out string json, out Error error);

            Assert.IsTrue(ok, error.Message);
            var payload = JObject.Parse(json);
            Assert.AreEqual("SHARE", (string)payload["intent"]);
            Assert.AreEqual("data:image/png;base64,AAAA", (string)payload["image"]);
            Assert.AreEqual("Look at this", (string)payload["text"]);
            Assert.AreEqual("SPRING25", (string)payload["data"]["coupon"]);
            Assert.AreEqual(JTokenType.Object, payload["data"]["room"].Type);
            Assert.AreEqual("r1", (string)payload["data"]["room"]["id"]);
            Assert.AreEqual(4, payload.Count, json);
        }

        [Test]
        public void Payload_WithOnlyAnImage_OmitsTheAbsentFields()
        {
            bool ok = Yes2SDKContext.TrySerializeSharePayload(null, "data:image/png;base64,AAAA", null,
                out string json, out Error error);

            Assert.IsTrue(ok, error.Message);
            var payload = JObject.Parse(json);
            Assert.AreEqual(2, payload.Count, json);
            Assert.AreEqual("SHARE", (string)payload["intent"]);
            Assert.AreEqual("data:image/png;base64,AAAA", (string)payload["image"]);
        }

        [Test]
        public void Payload_WithoutAnImage_IsJustTheIntent()
        {
            bool ok = Yes2SDKContext.TrySerializeSharePayload("", null, null, out string json, out Error error);

            Assert.IsTrue(ok, error.Message);
            var payload = JObject.Parse(json);
            Assert.AreEqual(1, payload.Count, json);
            Assert.AreEqual("SHARE", (string)payload["intent"]);
        }

        private static IEnumerable<object> UnserializableDataValues()
        {
            var cyclic = new Dictionary<string, object>();
            cyclic["self"] = cyclic;
            yield return cyclic;
            // A unit vector: its "normalized" property equals itself, so the serializer reports a loop.
            yield return Vector3.right;
        }

        [Test]
        public void Payload_WithUnserializableData_ReturnsInvalidParams(
            [ValueSource(nameof(UnserializableDataValues))] object value)
        {
            var data = new Dictionary<string, object> { ["value"] = value };

            bool ok = Yes2SDKContext.TrySerializeSharePayload(null, "data:image/png;base64,AAAA", data,
                out string json, out Error error);

            Assert.IsFalse(ok);
            Assert.IsNull(json);
            Assert.AreEqual(ErrorCode.InvalidParams, error.ErrorCode);
        }

        // --- Image normalization ---

        [Test]
        public void RawBase64Image_GetsThePngDataUrlPrefix()
        {
            Assert.AreEqual("data:image/png;base64,iVBORw0KGgo=", Yes2SDKContext.NormalizeImage("iVBORw0KGgo="));
        }

        [Test]
        public void DataUrlImage_IsKeptAsIs()
        {
            Assert.AreEqual("data:image/png;base64,iVBORw0KGgo=", Yes2SDKContext.NormalizeImage("data:image/png;base64,iVBORw0KGgo="));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void MissingImage_NormalizesToNull(string image)
        {
            Assert.IsNull(Yes2SDKContext.NormalizeImage(image));
        }

        // --- Synchronous InvalidParams, no pending request left behind ---

        [Test]
        public void ShareImageAsync_WithUnserializableData_FailsSynchronouslyWithInvalidParams()
        {
            var cyclic = new Dictionary<string, object>();
            cyclic["self"] = cyclic;
            Error received = default;
            int errors = 0;

            Yes2SDK.Context.ShareImageAsync(
                new ContextShareOptions { Text = "t", ImageDataUrl = "iVBORw0KGgo=", Data = cyclic },
                () => Assert.Fail("success"), e => { errors++; received = e; });

            Assert.AreEqual(1, errors);
            Assert.AreEqual(ErrorCode.InvalidParams, received.ErrorCode);
            Assert.AreEqual(0, Yes2SDKContext.PendingCountForTests);
        }

        [Test]
        public void ShareImageAsync_WithNullOptions_FailsSynchronouslyWithInvalidParams()
        {
            Error received = default;
            int errors = 0;

            Yes2SDK.Context.ShareImageAsync(null, () => Assert.Fail("success"), e => { errors++; received = e; });

            Assert.AreEqual(1, errors);
            Assert.AreEqual(ErrorCode.InvalidParams, received.ErrorCode);
            Assert.AreEqual(0, Yes2SDKContext.PendingCountForTests);
        }

        // --- Public API on the Editor path (no popups headless, so the mock is off) ---

        [Test]
        public void PublicApi_WithoutTheMock_ReportsFeatureNotSupportedOnce()
        {
            var codes = new List<ErrorCode>();
            int successes = 0;
            var texture = new Texture2D(2, 2);
            try
            {
                Yes2SDK.Context.ShareAsync("t", "iVBORw0KGgo=", () => successes++, e => codes.Add(e.ErrorCode));
                Yes2SDK.Context.ShareAsync("t", null, () => successes++, e => codes.Add(e.ErrorCode));
                Yes2SDK.Context.ShareImageAsync(
                    new ContextShareOptions
                    {
                        ImageDataUrl = Yes2SDKImage.ToPngDataUrl(texture),
                        Text = "t",
                        Data = new Dictionary<string, object> { ["k"] = 1 }
                    },
                    () => successes++, e => codes.Add(e.ErrorCode));
                Yes2SDK.Context.ShareImageAsync(new ContextShareOptions(), () => successes++, e => codes.Add(e.ErrorCode));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            Assert.AreEqual(0, successes);
            Assert.AreEqual(new[]
            {
                ErrorCode.FeatureNotSupported, ErrorCode.FeatureNotSupported,
                ErrorCode.FeatureNotSupported, ErrorCode.FeatureNotSupported
            }, codes);
            Assert.AreEqual(0, Yes2SDKContext.PendingCountForTests);
        }

        /// <summary>
        /// The call shapes 2.9.0 accepted must keep compiling (a literal null in
        /// any position included), so this test is also a compile-time guard
        /// against adding an overload that makes them ambiguous.
        /// </summary>
        [Test]
        public void ShareAsync_CallShapesFrom290_StillCompileAndRun()
        {
            var codes = new List<ErrorCode>();

            Yes2SDK.Context.ShareAsync("t", "img", null);
            Yes2SDK.Context.ShareAsync(null, "img");
            Yes2SDK.Context.ShareAsync(null, null);
            Yes2SDK.Context.ShareAsync("t", "img", () => { }, e => codes.Add(e.ErrorCode));
            Yes2SDK.Context.ShareAsync(null, null, null, e => codes.Add(e.ErrorCode));

            Assert.AreEqual(new[] { ErrorCode.FeatureNotSupported, ErrorCode.FeatureNotSupported }, codes);
            Assert.AreEqual(0, Yes2SDKContext.PendingCountForTests);
        }

        [Test]
        public void TaskOverloads_FaultWithTheError()
        {
            var share = Yes2SDK.Context.ShareImageAsync(
                new ContextShareOptions { ImageDataUrl = "iVBORw0KGgo=" }, CancellationToken.None);
            var invalid = Yes2SDK.Context.ShareImageAsync(null, CancellationToken.None);

            Assert.IsTrue(share.IsFaulted);
            Assert.AreEqual(ErrorCode.FeatureNotSupported, ((Yes2SDKException)share.Exception.InnerException).ErrorCode);
            Assert.IsTrue(invalid.IsFaulted);
            Assert.AreEqual(ErrorCode.InvalidParams, ((Yes2SDKException)invalid.Exception.InnerException).ErrorCode);
        }

        // --- Editor mock (called directly: CanShowPopups is false in batch mode) ---

        [Test]
        public void MockShare_Succeeds()
        {
            int successes = 0;

            Yes2SDKContext.MockShareForTests("t", "iVBORw0KGgo=", new Dictionary<string, object> { ["k"] = 1 },
                () => successes++, e => Assert.Fail(e.Message));

            Assert.AreEqual(1, successes);
            Assert.AreEqual(0, Yes2SDKContext.PendingCountForTests);
        }

        // --- Other Context methods stay unsupported ---

        [Test]
        public void OtherContextMethods_StillReportFeatureNotSupported()
        {
            var codes = new List<ErrorCode>();
            int successes = 0;

            Yes2SDK.Context.SwitchAsync("c1", () => successes++, e => codes.Add(e.ErrorCode));
            Yes2SDK.Context.ChooseAsync(() => successes++, e => codes.Add(e.ErrorCode));
            Yes2SDK.Context.CreateAsync("p1", () => successes++, e => codes.Add(e.ErrorCode));

            Assert.AreEqual(0, successes);
            Assert.AreEqual(new[] { ErrorCode.FeatureNotSupported, ErrorCode.FeatureNotSupported, ErrorCode.FeatureNotSupported }, codes);
            Assert.IsNull(Yes2SDK.Context.GetContext());
            Assert.IsFalse(Yes2SDK.Context.IsSupported());
        }
    }
}
