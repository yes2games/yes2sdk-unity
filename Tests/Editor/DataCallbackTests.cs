using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers confirmed-write callback ownership: each SetStringAsync / FlushAsync
    /// result reaches only the call that made it, and a finished call is cleaned
    /// up before game code runs, so a callback can start the next save.
    ///
    /// Slow platform confirmations cannot happen headless, so these cases
    /// register requests without sending them and deliver the bridge messages
    /// by hand, in the shape Yes2SDKData.jslib sends them.
    /// </summary>
    public class DataCallbackTests
    {
        // Operation is internal and a public test method cannot take it as a
        // parameter, so cases are parameterized by name and parsed back.
        private static readonly string[] AllOperations =
        {
            nameof(Yes2SDKData.Operation.SetString),
            nameof(Yes2SDKData.Operation.Flush)
        };

        private static Yes2SDKData.Operation Op(string name)
        {
            return (Yes2SDKData.Operation)Enum.Parse(typeof(Yes2SDKData.Operation), name);
        }

        private List<string> _calls;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            Yes2SDKData.ResetDataStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKData.ResetDataStateForTests();
        }

        private int Register(Yes2SDKData.Operation op, string label)
        {
            return Yes2SDKData.RegisterForTests(op,
                saved => _calls.Add($"{label}:{saved}"),
                error => _calls.Add($"{label}:error:{error.Code}"));
        }

        private static void Deliver(Yes2SDKData.Operation op, int requestId, bool saved)
        {
            Yes2SDKData.HandleSuccessMessage(op, requestId + "|" + (saved ? "true" : "false"));
        }

        private static void DeliverError(Yes2SDKData.Operation op, int requestId, string code)
        {
            Yes2SDKData.HandleErrorMessage(op,
                requestId + "|{\"code\":\"" + code + "\",\"message\":\"m\",\"context\":\"c\"}",
                Bridge.ParseError);
        }

        [Test]
        public void OverlappingCalls_EachGetTheirOwnResult([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int a = Register(op, "A");
            int b = Register(op, "B");

            Deliver(op, b, false);
            Deliver(op, a, true);

            Assert.AreEqual(new[] { "B:False", "A:True" }, _calls);
            Assert.AreEqual(0, Yes2SDKData.PendingCountForTests);
        }

        [Test]
        public void ErrorForOneCall_DoesNotCompleteTheOther([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int a = Register(op, "A");
            int b = Register(op, "B");

            DeliverError(op, a, "NetworkError");
            Assert.AreEqual(1, Yes2SDKData.PendingCountForTests, "B must still be pending");

            Deliver(op, b, true);
            Assert.AreEqual(new[] { "A:error:NetworkError", "B:True" }, _calls);
        }

        [Test]
        public void DuplicateResponse_IsDropped([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int a = Register(op, "A");

            Deliver(op, a, true);
            Deliver(op, a, false);
            DeliverError(op, a, "Late");

            Assert.AreEqual(new[] { "A:True" }, _calls);
        }

        [Test]
        public void ResponseOnTheOtherOperation_IsDropped()
        {
            int flush = Register(Yes2SDKData.Operation.Flush, "flush");

            Deliver(Yes2SDKData.Operation.SetString, flush, true);
            Assert.IsEmpty(_calls);

            Deliver(Yes2SDKData.Operation.Flush, flush, true);
            Assert.AreEqual(new[] { "flush:True" }, _calls);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("true")]
        [TestCase("|true")]
        [TestCase("x|true")]
        public void MessageWithoutARequestId_IsDropped(string message)
        {
            Register(Yes2SDKData.Operation.SetString, "A");

            Yes2SDKData.HandleSuccessMessage(Yes2SDKData.Operation.SetString, message);

            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKData.PendingCountForTests);
        }

        [Test]
        public void SaveStartedFromACallback_KeepsItsCallbacks([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = 0;
            int a = Yes2SDKData.RegisterForTests(op,
                _ =>
                {
                    _calls.Add("A");
                    b = Register(op, "B");
                },
                _ => { });

            Deliver(op, a, true);
            Deliver(op, b, true);

            Assert.AreEqual(new[] { "A", "B:True" }, _calls);
        }

        [Test]
        public void ThrowingCallback_StillCompletesItsRequest()
        {
            int b = Register(Yes2SDKData.Operation.SetString, "B");
            int a = Yes2SDKData.RegisterForTests(Yes2SDKData.Operation.SetString,
                _ => throw new InvalidOperationException("game code failed"),
                _ => { });

            Assert.Throws<InvalidOperationException>(() => Deliver(Yes2SDKData.Operation.SetString, a, true));

            Deliver(Yes2SDKData.Operation.SetString, a, true);
            Deliver(Yes2SDKData.Operation.SetString, b, true);
            Assert.AreEqual(new[] { "B:True" }, _calls);
            Assert.AreEqual(0, Yes2SDKData.PendingCountForTests);
        }

        [Test]
        public void PublicApi_EditorPath_CompletesEachCallOnce()
        {
            // PlayerPrefs is not touched in the stubbed runner, but it is in
            // Unity; use a key this suite owns and remove it afterwards.
            const string key = "__yes2sdk_test_data_callback";
            int set = 0, flush = 0;

            Yes2SDK.Data.SetStringAsync(key, "v", saved => set++, _ => set++);
            Yes2SDK.Data.FlushAsync(saved => flush++, _ => flush++);
            Yes2SDK.Data.DeleteKey(key);

            Assert.AreEqual(1, set);
            Assert.AreEqual(1, flush);
            Assert.AreEqual(0, Yes2SDKData.PendingCountForTests);
        }
    }
}
