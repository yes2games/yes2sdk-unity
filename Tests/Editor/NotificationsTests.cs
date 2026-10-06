using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the notifications module: request routing (each response reaches
    /// only the call that made it), the options JSON sent to the platform, the
    /// validation the Editor mock applies, the legacy overload and the mock.
    ///
    /// Delayed platform responses cannot happen headless, so routing cases
    /// register requests without sending them and deliver the bridge messages
    /// by hand, through the same envelope parsing the Bridge uses.
    /// </summary>
    public class NotificationsTests
    {
        private static readonly string[] AllOperations =
        {
            nameof(Yes2SDKNotifications.Operation.Schedule),
            nameof(Yes2SDKNotifications.Operation.Cancel),
            nameof(Yes2SDKNotifications.Operation.CancelAll)
        };

        private static Yes2SDKNotifications.Operation Op(string name)
        {
            return (Yes2SDKNotifications.Operation)Enum.Parse(typeof(Yes2SDKNotifications.Operation), name);
        }

        private List<string> _calls;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            Yes2SDKNotifications.ResetNotificationsStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKNotifications.ResetNotificationsStateForTests();
        }

        private int Register(Yes2SDKNotifications.Operation operation, string label)
        {
            return Yes2SDKNotifications.RegisterForTests(operation,
                data => _calls.Add($"{label}:success:{data}"),
                error => _calls.Add($"{label}:error:{error.Code}"));
        }

        private static void DeliverSuccess(Yes2SDKNotifications.Operation operation, int requestId, string payload)
        {
            Yes2SDKNotifications.HandleSuccessMessage(operation, Yes2SDKNotifications.EnvelopeForTests(requestId, payload));
        }

        private static void DeliverError(Yes2SDKNotifications.Operation operation, int requestId, string code)
        {
            string json = "{\"code\":\"" + code + "\",\"message\":\"m\",\"context\":\"c\"}";
            Yes2SDKNotifications.HandleErrorMessage(operation, Yes2SDKNotifications.EnvelopeForTests(requestId, json), Bridge.ParseError);
        }

        private static NotificationOptions Valid()
        {
            return new NotificationOptions { Title = "Your crops are ready", Body = "Come back and harvest", DelaySeconds = 60 };
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
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
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
            Assert.AreEqual(1, Yes2SDKNotifications.PendingCountForTests, "B must still be pending after A's error");

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
            int cancel = Register(Yes2SDKNotifications.Operation.Cancel, "cancel");

            DeliverSuccess(Yes2SDKNotifications.Operation.CancelAll, cancel, "wrong");
            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKNotifications.PendingCountForTests, "a mismatched response must not remove the request");

            DeliverSuccess(Yes2SDKNotifications.Operation.Cancel, cancel, "");
            Assert.AreEqual(new[] { "cancel:success:" }, _calls);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("|{}")]
        [TestCase("abc|{}")]
        [TestCase("-1|{}")]
        public void MessageWithoutARequestId_IsDropped(string message)
        {
            Register(Yes2SDKNotifications.Operation.Schedule, "A");

            Yes2SDKNotifications.HandleSuccessMessage(Yes2SDKNotifications.Operation.Schedule, message);

            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKNotifications.PendingCountForTests);
        }

        [Test]
        public void StartingTheNextRequestFromASuccessCallback_KeepsItsCallbacks([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = 0;
            int a = Yes2SDKNotifications.RegisterForTests(op,
                _ =>
                {
                    _calls.Add("A:success");
                    b = Register(op, "B");
                },
                _ => _calls.Add("A:error"));

            DeliverSuccess(op, a, "a");
            DeliverSuccess(op, b, "b");

            Assert.AreEqual(new[] { "A:success", "B:success:b" }, _calls);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
        }

        [Test]
        public void ThrowingCallback_StillCompletesItsRequest([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = Register(op, "B");
            int a = Yes2SDKNotifications.RegisterForTests(op,
                _ => throw new InvalidOperationException("game code failed"),
                _ => { });

            Assert.Throws<InvalidOperationException>(() => DeliverSuccess(op, a, "a"));

            Assert.AreEqual(1, Yes2SDKNotifications.PendingCountForTests, "A is removed even though its callback threw");
            DeliverSuccess(op, a, "again");
            DeliverSuccess(op, b, "b");
            Assert.AreEqual(new[] { "B:success:b" }, _calls);
        }

        [Test]
        public void ErrorCode_FromThePlatform_ReachesTheCaller()
        {
            Error received = default;
            int a = Yes2SDKNotifications.RegisterScheduleForTests(_ => Assert.Fail("no success expected"), e => received = e);

            DeliverError(Yes2SDKNotifications.Operation.Schedule, a, "PLAYER_NOT_AUTHENTICATED");

            Assert.AreEqual("PLAYER_NOT_AUTHENTICATED", received.Code);
        }

        // --- Typed schedule result ---

        [Test]
        public void ScheduleSuccess_ParsesTheScheduledNotification()
        {
            ScheduledNotification received = default;
            int a = Yes2SDKNotifications.RegisterScheduleForTests(n => received = n, e => Assert.Fail(e.ToString()));

            DeliverSuccess(Yes2SDKNotifications.Operation.Schedule, a,
                "{\"id\":\"daily|1\",\"title\":\"T\",\"body\":\"B\",\"scheduledAt\":1767225600000}");

            Assert.AreEqual("daily|1", received.Id);
            Assert.AreEqual("T", received.Title);
            Assert.AreEqual("B", received.Body);
            Assert.AreEqual(1767225600000L, received.ScheduledAt);
        }

        [TestCase("")]
        [TestCase("null")]
        [TestCase("not json SECRET-PAYLOAD")]
        [TestCase("{\"id\":\"SECRET-PAYLOAD\",\"scheduledAt\":\"later\"}")]
        public void UnreadableScheduleResult_IsReportedAsUnknownWithoutThePayload(string payload)
        {
            Error received = default;
            int a = Yes2SDKNotifications.RegisterScheduleForTests(_ => Assert.Fail("no success expected"), e => received = e);

            DeliverSuccess(Yes2SDKNotifications.Operation.Schedule, a, payload);

            Assert.AreEqual("Unknown", received.Code);
            Assert.IsFalse(string.IsNullOrEmpty(received.Message));
            StringAssert.DoesNotContain("SECRET-PAYLOAD", received.Message);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
        }

        // --- Options JSON ---

        [Test]
        public void Serialization_OmitsUnsetFields()
        {
            string json = Valid().ToJson();

            StringAssert.Contains("\"title\":\"Your crops are ready\"", json);
            StringAssert.Contains("\"body\":\"Come back and harvest\"", json);
            StringAssert.Contains("\"delaySeconds\":60", json);
            foreach (string absent in new[] { "id", "scheduledInDays", "ctaText", "priority", "imageAssetId", "imageDataUrl", "iconUrl", "data", "null" })
            {
                StringAssert.DoesNotContain("\"" + absent + "\"", json);
            }
            StringAssert.DoesNotContain("null", json);
        }

        [Test]
        public void Serialization_ScheduledInDaysZero_IsSent()
        {
            var options = new NotificationOptions { Title = "t", Body = "b", ScheduledInDays = 0 };

            string json = options.ToJson();

            StringAssert.Contains("\"scheduledInDays\":0", json);
            StringAssert.DoesNotContain("delaySeconds", json);
        }

        [TestCase(NotificationPriority.Low, "low")]
        [TestCase(NotificationPriority.Medium, "medium")]
        [TestCase(NotificationPriority.High, "high")]
        [TestCase(NotificationPriority.Critical, "critical")]
        public void Serialization_PriorityIsLowercase(NotificationPriority priority, string expected)
        {
            var options = Valid();
            options.Priority = priority;

            StringAssert.Contains("\"priority\":\"" + expected + "\"", options.ToJson());
        }

        [Test]
        public void Serialization_AllFields_UsePlatformKeyNames()
        {
            var options = new NotificationOptions
            {
                Id = "daily",
                Title = "t",
                Body = "b",
                ScheduledInDays = 2,
                CtaText = "Play",
                ImageAssetId = "asset-1",
                IconUrl = "https://example.com/i.png",
                Data = new Dictionary<string, object> { ["level"] = 3 }
            };

            string json = options.ToJson();

            StringAssert.Contains("\"id\":\"daily\"", json);
            StringAssert.Contains("\"scheduledInDays\":2", json);
            StringAssert.Contains("\"ctaText\":\"Play\"", json);
            StringAssert.Contains("\"imageAssetId\":\"asset-1\"", json);
            StringAssert.Contains("\"iconUrl\":\"https://example.com/i.png\"", json);
            StringAssert.Contains("\"data\":{\"level\":3}", json);
        }

        [Test]
        public void Serialization_ImageDataUrl_UsesItsKey()
        {
            var options = Valid();
            options.ImageDataUrl = "data:image/png;base64,AAAA";

            StringAssert.Contains("\"imageDataUrl\":\"data:image/png;base64,AAAA\"", options.ToJson());
        }

        [Test]
        public void Serialization_NullBody_IsSentAsEmptyString()
        {
            var options = new NotificationOptions { Title = "t", DelaySeconds = 5 };

            StringAssert.Contains("\"body\":\"\"", options.ToJson());
        }

        // --- Validation (mirrors the platform layer; used by the Editor mock) ---

        [Test]
        public void TryValidate_AcceptsADelay()
        {
            Assert.IsTrue(NotificationOptions.TryValidate(Valid(), out string message), message);
        }

        [TestCase(0)]
        [TestCase(7)]
        public void TryValidate_AcceptsScheduledInDaysInRange(int days)
        {
            var options = new NotificationOptions { Title = "t", Body = "b", ScheduledInDays = days };

            Assert.IsTrue(NotificationOptions.TryValidate(options, out string message), message);
        }

        private static IEnumerable<TestCaseData> InvalidOptions()
        {
            yield return new TestCaseData(null, "options must be a valid object").SetName("Null options");
            yield return new TestCaseData(new NotificationOptions { Title = "", Body = "b", DelaySeconds = 1 },
                "options.title must be a non-empty string").SetName("Empty title");
            yield return new TestCaseData(new NotificationOptions { Title = "  ", Body = "b", DelaySeconds = 1 },
                "options.title must be a non-empty string").SetName("Blank title");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", DelaySeconds = 1, ImageAssetId = "a", ImageDataUrl = "data:image/png;base64,AA" },
                "Provide at most one of options.imageAssetId or options.imageDataUrl").SetName("Both images");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", DelaySeconds = 1, ImageAssetId = "" },
                "options.imageAssetId must be a non-empty string when provided").SetName("Empty image asset id");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", DelaySeconds = 1, ImageDataUrl = " " },
                "options.imageDataUrl must be a non-empty string when provided").SetName("Blank image data url");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", DelaySeconds = 1, ScheduledInDays = 1 },
                "Provide exactly one of options.delaySeconds or options.scheduledInDays").SetName("Both delays");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b" },
                "Provide exactly one of options.delaySeconds or options.scheduledInDays").SetName("Neither delay");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", DelaySeconds = 0 },
                "options.delaySeconds must be a positive number").SetName("Zero delay");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", DelaySeconds = -5 },
                "options.delaySeconds must be a positive number").SetName("Negative delay");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", ScheduledInDays = 8 },
                "options.scheduledInDays must be an integer from 0 to 7").SetName("Days 8");
            yield return new TestCaseData(new NotificationOptions { Title = "t", Body = "b", ScheduledInDays = -1 },
                "options.scheduledInDays must be an integer from 0 to 7").SetName("Days negative");
        }

        [TestCaseSource(nameof(InvalidOptions))]
        public void TryValidate_RejectsWithThePlatformMessage(NotificationOptions options, string expected)
        {
            Assert.IsFalse(NotificationOptions.TryValidate(options, out string message));
            Assert.AreEqual(expected, message);
        }

        // --- Platform limits (applied by the Editor mock, enforced by the platform at runtime) ---

        private static NotificationOptions Limits(Action<NotificationOptions> change)
        {
            var options = Valid();
            change(options);
            return options;
        }

        private static IEnumerable<TestCaseData> WithinLimits()
        {
            yield return new TestCaseData(Valid()).SetName("Plain options");
            yield return new TestCaseData(Limits(o => o.Body = new string('b', 2000))).SetName("Body 2000");
            yield return new TestCaseData(Limits(o => o.Title = new string('t', 200))).SetName("Title 200");
            yield return new TestCaseData(Limits(o => o.CtaText = new string('c', 50))).SetName("Cta 50");
            yield return new TestCaseData(Limits(o => o.CtaText = "c")).SetName("Cta 1");
            yield return new TestCaseData(Limits(o => o.DelaySeconds = 7 * 86_400)).SetName("Delay exactly 7 days");
            yield return new TestCaseData(Limits(o => { o.DelaySeconds = null; o.ScheduledInDays = 7; })).SetName("Days 7");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "data:image/png;base64,AAAA")).SetName("Png data url");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "data:image/jpeg;base64,AAAA")).SetName("Jpeg data url");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "data:image/webp;base64,AAAA")).SetName("Webp data url");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "data:image/png;base64," + new string('A', Yes2SDKImage.MaxDataUrlLength - 22))).SetName("Data url at the size limit");
            yield return new TestCaseData(Limits(o => o.Id = "daily")).SetName("Given id");
        }

        [TestCaseSource(nameof(WithinLimits))]
        public void TryValidatePlatformLimits_Accepts(NotificationOptions options)
        {
            Assert.IsTrue(NotificationOptions.TryValidatePlatformLimits(options, out string message), message);
        }

        private static IEnumerable<TestCaseData> OverLimits()
        {
            yield return new TestCaseData(Limits(o => o.Id = ""), NotificationOptions.EmptyIdMessage).SetName("Empty id");
            yield return new TestCaseData(Limits(o => o.Body = null), NotificationOptions.BodyLengthMessage).SetName("Null body");
            yield return new TestCaseData(Limits(o => o.Body = ""), NotificationOptions.BodyLengthMessage).SetName("Empty body");
            yield return new TestCaseData(Limits(o => o.Body = new string('b', 2001)), NotificationOptions.BodyLengthMessage).SetName("Body 2001");
            yield return new TestCaseData(Limits(o => o.Title = new string('t', 201)), NotificationOptions.TitleLengthMessage).SetName("Title 201");
            yield return new TestCaseData(Limits(o => o.CtaText = ""), NotificationOptions.CtaLengthMessage).SetName("Empty cta");
            yield return new TestCaseData(Limits(o => o.CtaText = new string('c', 51)), NotificationOptions.CtaLengthMessage).SetName("Cta 51");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "data:image/gif;base64,AAAA"), NotificationOptions.ImageFormatMessage).SetName("Gif data url");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "DATA:IMAGE/PNG;BASE64,AAAA"), NotificationOptions.ImageFormatMessage).SetName("Uppercase prefix");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "https://example.com/a.png"), NotificationOptions.ImageFormatMessage).SetName("Plain url");
            yield return new TestCaseData(Limits(o => o.ImageDataUrl = "data:image/png;base64," + new string('A', Yes2SDKImage.MaxDataUrlLength - 21)), NotificationOptions.ImageSizeMessage).SetName("Data url over the size limit");
            yield return new TestCaseData(Limits(o => o.DelaySeconds = 7 * 86_400 + 1), NotificationOptions.DelayTooLongMessage).SetName("Delay over 7 days");
            yield return new TestCaseData(Limits(o => o.DelaySeconds = int.MaxValue), NotificationOptions.DelayTooLongMessage).SetName("Delay int max");
        }

        [TestCaseSource(nameof(OverLimits))]
        public void TryValidatePlatformLimits_Rejects(NotificationOptions options, string expected)
        {
            Assert.IsFalse(NotificationOptions.TryValidatePlatformLimits(options, out string message));
            Assert.AreEqual(expected, message);
        }

        [Test]
        public void PlatformLimitMessages_NameNoPlatform()
        {
            string[] messages =
            {
                NotificationOptions.EmptyIdMessage, NotificationOptions.BodyLengthMessage, NotificationOptions.TitleLengthMessage,
                NotificationOptions.CtaLengthMessage, NotificationOptions.ImageFormatMessage, NotificationOptions.ImageSizeMessage,
                NotificationOptions.DelayTooLongMessage
            };
            foreach (string message in messages)
            {
                StringAssert.DoesNotContain(" on ", message);
            }
        }

        [Test]
        public void ScheduledInDays_IsAWholeNumberType_SoFractionalDaysCannotBeSent()
        {
            Assert.AreEqual(typeof(int?), typeof(NotificationOptions).GetField(nameof(NotificationOptions.ScheduledInDays)).FieldType);
        }

        // --- Public API on the Editor path (mock inactive outside Play Mode) ---

        [Test]
        public void ScheduleAsync_NullOptions_FailsSynchronouslyWithInvalidParams()
        {
            var errors = new List<Error>();

            Yes2SDK.Notifications.ScheduleAsync(null, _ => Assert.Fail("no success expected"), errors.Add);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("InvalidParams", errors[0].Code);
            Assert.AreEqual(ErrorCode.InvalidParams, errors[0].ErrorCode);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
        }

        private static NotificationOptions WithCyclicData()
        {
            var options = Valid();
            options.Data = new Dictionary<string, object>();
            options.Data["self"] = options.Data;
            return options;
        }

        [Test]
        public void ScheduleAsync_CyclicData_FailsSynchronouslyWithInvalidParams()
        {
            var errors = new List<Error>();

            Yes2SDK.Notifications.ScheduleAsync(WithCyclicData(), _ => Assert.Fail("no success expected"), errors.Add);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("InvalidParams", errors[0].Code);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
        }

        [Test]
        public void PublicApi_WithoutTheMock_ReportsFeatureNotSupportedOnce()
        {
            var codes = new List<string>();

            Yes2SDK.Notifications.ScheduleAsync(Valid(), _ => codes.Add("schedule:ok"), e => codes.Add("schedule:" + e.Code));
            Yes2SDK.Notifications.CancelAsync("n1", () => codes.Add("cancel:ok"), e => codes.Add("cancel:" + e.Code));
            Yes2SDK.Notifications.CancelAllAsync(() => codes.Add("all:ok"), e => codes.Add("all:" + e.Code));

            Assert.AreEqual(new[] { "schedule:FeatureNotSupported", "cancel:FeatureNotSupported", "all:FeatureNotSupported" }, codes);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
            Assert.IsFalse(Yes2SDK.Notifications.IsSupported());
        }

        [Test]
        public void TaskOverload_NullOptions_FaultsWithTheSdkException()
        {
            var task = Yes2SDK.Notifications.ScheduleAsync(null, CancellationToken.None);

            Assert.IsTrue(task.IsFaulted);
            var ex = task.Exception.InnerException as Yes2SDKException;
            Assert.IsNotNull(ex);
            Assert.AreEqual("InvalidParams", ex.SdkError.Code);
        }

        [Test]
        public void TaskOverloads_CancelWithoutTheMock_Fault()
        {
            Assert.IsTrue(Yes2SDK.Notifications.CancelAsync("n1", CancellationToken.None).IsFaulted);
            Assert.IsTrue(Yes2SDK.Notifications.CancelAllAsync(CancellationToken.None).IsFaulted);
        }

        // --- Legacy overload ---

        [Test]
        public void Legacy_ForwardsTheDelayAndPassesTheIdToOnSuccess()
        {
            NotificationOptions sent = null;
            string id = null;

            Yes2SDKNotifications.ScheduleLegacy("Title", "Body", 120, "{\"level\":4}",
                result => id = result, e => Assert.Fail(e.ToString()),
                (options, onSuccess, onError) =>
                {
                    sent = options;
                    onSuccess(new ScheduledNotification { Id = "n-42", Title = options.Title, Body = options.Body, ScheduledAt = 1 });
                });

            Assert.AreEqual("n-42", id);
            Assert.AreEqual("Title", sent.Title);
            Assert.AreEqual("Body", sent.Body);
            Assert.AreEqual(120, sent.DelaySeconds);
            Assert.IsNull(sent.ScheduledInDays);
            Assert.IsNotNull(sent.Data);
            StringAssert.Contains("\"data\":{\"level\":4}", sent.ToJson());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[1,2]")]
        public void Legacy_EmptyOrInvalidData_IsDropped(string dataJson)
        {
            NotificationOptions sent = null;

            Yes2SDKNotifications.ScheduleLegacy("t", "b", 10, dataJson, null, null,
                (options, onSuccess, onError) => sent = options);

            Assert.IsNotNull(sent);
            Assert.IsNull(sent.Data);
        }

        [Test]
        public void Legacy_ForwardsErrors()
        {
            string code = null;

            Yes2SDKNotifications.ScheduleLegacy("t", "b", 10, null, _ => Assert.Fail("no success expected"), e => code = e.Code,
                (options, onSuccess, onError) => onError(new Error { Code = "PlatformError" }));

            Assert.AreEqual("PlatformError", code);
        }

        [Test]
        public void Legacy_PublicOverload_WithoutTheMock_ReportsFeatureNotSupported()
        {
            var codes = new List<string>();

            Yes2SDK.Notifications.ScheduleAsync("t", "b", 30, null, _ => codes.Add("ok"), e => codes.Add(e.Code));

            Assert.AreEqual(new[] { "FeatureNotSupported" }, codes);
        }

        // --- Editor mock ---

        [Test]
        public void Mock_Schedule_ReturnsTheGivenIdAndComputedTime()
        {
            ScheduledNotification result = default;
            long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var options = Valid();
            options.Id = "daily";

            Yes2SDKNotifications.MockScheduleForTests(options, true, n => result = n, e => Assert.Fail(e.ToString()));

            long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.AreEqual("daily", result.Id);
            Assert.AreEqual(options.Title, result.Title);
            Assert.AreEqual(options.Body, result.Body);
            Assert.That(result.ScheduledAt, Is.InRange(before + 60_000, after + 60_000));
        }

        [Test]
        public void Mock_Schedule_InDays_UsesWholeDays()
        {
            ScheduledNotification result = default;
            long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Yes2SDKNotifications.MockScheduleForTests(new NotificationOptions { Title = "t", Body = "b", ScheduledInDays = 3 }, true,
                n => result = n, e => Assert.Fail(e.ToString()));

            long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(result.ScheduledAt, Is.InRange(before + 3 * 86_400_000L, after + 3 * 86_400_000L));
        }

        [Test]
        public void Mock_Schedule_GeneratesAnIdWhenNoneIsGiven()
        {
            var ids = new List<string>();

            Yes2SDKNotifications.MockScheduleForTests(Valid(), true, n => ids.Add(n.Id), e => Assert.Fail(e.ToString()));
            Yes2SDKNotifications.MockScheduleForTests(Valid(), true, n => ids.Add(n.Id), e => Assert.Fail(e.ToString()));

            Assert.AreEqual(2, ids.Count);
            Assert.IsFalse(string.IsNullOrEmpty(ids[0]));
            Assert.AreNotEqual(ids[0], ids[1]);
            Assert.AreEqual(2, Yes2SDKNotifications.MockScheduledForTests.Count);
        }

        [Test]
        public void Mock_Schedule_SameIdReplaces()
        {
            var first = Valid();
            first.Id = "daily";
            var second = Valid();
            second.Id = "daily";
            second.Title = "Updated";

            Yes2SDKNotifications.MockScheduleForTests(first, true, null, null);
            Yes2SDKNotifications.MockScheduleForTests(second, true, null, null);

            Assert.AreEqual(1, Yes2SDKNotifications.MockScheduledForTests.Count);
            Assert.AreEqual("Updated", Yes2SDKNotifications.MockScheduledForTests[0].Title);
        }

        [Test]
        public void Mock_Schedule_InvalidOptions_FailWithThePlatformMessage()
        {
            Error received = default;

            Yes2SDKNotifications.MockScheduleForTests(new NotificationOptions { Title = "t", Body = "b", DelaySeconds = 5, ScheduledInDays = 1 }, true,
                _ => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("InvalidParams", received.Code);
            Assert.AreEqual("Provide exactly one of options.delaySeconds or options.scheduledInDays", received.Message);
            Assert.AreEqual(0, Yes2SDKNotifications.MockScheduledForTests.Count);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
        }

        [Test]
        public void Mock_Schedule_CyclicData_FailsWithInvalidParams()
        {
            Error received = default;

            Yes2SDKNotifications.MockScheduleForTests(WithCyclicData(), true, _ => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("InvalidParams", received.Code);
            Assert.AreEqual(0, Yes2SDKNotifications.MockScheduledForTests.Count);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
        }

        [Test]
        public void Mock_Schedule_Guest_FailsWithPlayerNotAuthenticated()
        {
            Error received = default;

            Yes2SDKNotifications.MockScheduleForTests(Valid(), false, _ => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("PLAYER_NOT_AUTHENTICATED", received.Code);
            Assert.AreEqual(0, Yes2SDKNotifications.MockScheduledForTests.Count);
            Assert.AreEqual(0, Yes2SDKNotifications.PendingCountForTests);
        }

        [Test]
        public void Mock_Schedule_Guest_WithInvalidOptions_GetsInvalidParamsFirst()
        {
            Error received = default;

            Yes2SDKNotifications.MockScheduleForTests(new NotificationOptions { Title = " ", Body = "b", DelaySeconds = 5 }, false,
                _ => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("InvalidParams", received.Code);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Mock_Schedule_EmptyBody_FailsWithInvalidParams(string body)
        {
            Error received = default;
            var options = Valid();
            options.Body = body;

            Yes2SDKNotifications.MockScheduleForTests(options, true, _ => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("InvalidParams", received.Code);
            Assert.AreEqual(NotificationOptions.BodyLengthMessage, received.Message);
            Assert.AreEqual(0, Yes2SDKNotifications.MockScheduledForTests.Count);
        }

        [Test]
        public void Mock_Schedule_EmptyId_FailsWithInvalidParams()
        {
            Error received = default;
            var options = Valid();
            options.Id = "";

            Yes2SDKNotifications.MockScheduleForTests(options, true, _ => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("InvalidParams", received.Code);
            Assert.AreEqual(NotificationOptions.EmptyIdMessage, received.Message);
            Assert.AreEqual(0, Yes2SDKNotifications.MockScheduledForTests.Count);
        }

        [Test]
        public void Mock_Schedule_DelayOverSevenDays_FailsWithInvalidParams()
        {
            Error received = default;
            var options = Valid();
            options.DelaySeconds = 7 * 86_400 + 1;

            Yes2SDKNotifications.MockScheduleForTests(options, true, _ => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("InvalidParams", received.Code);
            Assert.AreEqual(NotificationOptions.DelayTooLongMessage, received.Message);
        }

        [Test]
        public void Mock_CancelAndCancelAll_EditTheSessionList()
        {
            var a = Valid();
            a.Id = "a";
            var b = Valid();
            b.Id = "b";
            Yes2SDKNotifications.MockScheduleForTests(a, true, null, null);
            Yes2SDKNotifications.MockScheduleForTests(b, true, null, null);
            int cancelled = 0;

            Yes2SDKNotifications.MockCancelForTests("a", () => cancelled++, e => Assert.Fail(e.ToString()));
            Assert.AreEqual(1, cancelled);
            Assert.AreEqual(1, Yes2SDKNotifications.MockScheduledForTests.Count);
            Assert.AreEqual("b", Yes2SDKNotifications.MockScheduledForTests[0].Id);

            Yes2SDKNotifications.MockCancelAllForTests(() => cancelled++, e => Assert.Fail(e.ToString()));
            Assert.AreEqual(2, cancelled);
            Assert.AreEqual(0, Yes2SDKNotifications.MockScheduledForTests.Count);
        }

        [Test]
        public void Mock_Cancel_BlankId_FailsWithInvalidParams()
        {
            Error received = default;

            Yes2SDKNotifications.MockCancelForTests(" ", () => Assert.Fail("no success expected"), e => received = e);

            Assert.AreEqual("InvalidParams", received.Code);
            Assert.AreEqual("notificationId must be a non-empty string", received.Message);
        }

        [Test]
        public void Reset_ClearsTheMockList()
        {
            Yes2SDKNotifications.MockScheduleForTests(Valid(), true, null, null);

            Yes2SDKNotifications.ResetNotificationsStateForTests();

            Assert.AreEqual(0, Yes2SDKNotifications.MockScheduledForTests.Count);
        }
    }

    /// <summary>Covers the texture to data URL helper used for notification and share images.</summary>
    public class ImageDataUrlTests
    {
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        [TearDown]
        public void TearDown()
        {
            foreach (var texture in _textures) UnityEngine.Object.DestroyImmediate(texture);
            _textures.Clear();
        }

        private Texture2D Make(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)i, (byte)(i * 7), (byte)(i * 13), 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            _textures.Add(texture);
            return texture;
        }

        [Test]
        public void ReadableTexture_BecomesALowercasePngDataUrl()
        {
            string url = Yes2SDKImage.ToPngDataUrl(Make(4));

            Assert.IsNotNull(url);
            StringAssert.StartsWith("data:image/png;base64,", url);
            Assert.Greater(url.Length, "data:image/png;base64,".Length);
        }

        [Test]
        public void NullTexture_ReturnsNull()
        {
            Assert.IsNull(Yes2SDKImage.ToPngDataUrl(null));
        }

        [Test]
        public void UnreadableTexture_ReturnsNull()
        {
            var texture = Make(4);
            texture.Apply(false, true);
            Assume.That(texture.isReadable, Is.False);

            Assert.IsNull(Yes2SDKImage.ToPngDataUrl(texture));
        }

        [Test]
        public void TooLargeResult_ReturnsNull()
        {
            Assert.IsNull(Yes2SDKImage.ToPngDataUrl(Make(16), 30));
        }

        [Test]
        public void SizeLimit_IsTwoMebibytes()
        {
            Assert.AreEqual(2 * 1024 * 1024, Yes2SDKImage.MaxDataUrlLength);
        }
    }
}
