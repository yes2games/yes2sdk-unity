using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers IAP callback ownership: each response reaches only the call that
    /// made it (#102), and a completed call is cleaned up before game code runs,
    /// so a callback can start the next call safely (#103).
    ///
    /// Delayed platform responses cannot happen headless, so these cases
    /// register requests without sending them and deliver the bridge messages
    /// by hand, in whatever order the case needs. The messages go through the
    /// same envelope parsing the Bridge uses for SendMessage from the jslib.
    /// </summary>
    public class IAPCallbackTests
    {
        // Operation is internal and a public test method cannot take it as a
        // parameter, so cases are parameterized by name and parsed back.
        private static readonly string[] AllOperations =
        {
            nameof(Yes2SDKIAP.Operation.GetCatalog),
            nameof(Yes2SDKIAP.Operation.Purchase),
            nameof(Yes2SDKIAP.Operation.GetPurchases),
            nameof(Yes2SDKIAP.Operation.ConsumePurchase)
        };

        private static Yes2SDKIAP.Operation Op(string name)
        {
            return (Yes2SDKIAP.Operation)Enum.Parse(typeof(Yes2SDKIAP.Operation), name);
        }

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

        private int Register(Yes2SDKIAP.Operation operation, string label)
        {
            return Yes2SDKIAP.RegisterForTests(operation,
                data => _calls.Add($"{label}:success:{data}"),
                error => _calls.Add($"{label}:error:{error.Code}"));
        }

        private static void DeliverSuccess(Yes2SDKIAP.Operation operation, int requestId, string payload)
        {
            Yes2SDKIAP.HandleSuccessMessage(operation, Yes2SDKIAP.EnvelopeForTests(requestId, payload));
        }

        private static void DeliverError(Yes2SDKIAP.Operation operation, int requestId, string code)
        {
            string json = "{\"code\":\"" + code + "\",\"message\":\"m\",\"context\":\"c\"}";
            Yes2SDKIAP.HandleErrorMessage(operation, Yes2SDKIAP.EnvelopeForTests(requestId, json), Bridge.ParseError);
        }

        // --- #102: responses reach only their own request ---

        [Test]
        public void DelayedResponse_CompletesOnlyItsOwnRequest([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            // A times out on the game side, B is the retry. A answers late.
            int a = Register(op, "A");
            int b = Register(op, "B");

            DeliverSuccess(op, a, "a");
            DeliverSuccess(op, b, "b");

            Assert.AreEqual(new[] { "A:success:a", "B:success:b" }, _calls);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
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
            Assert.AreEqual(1, Yes2SDKIAP.PendingCountForTests, "B must still be pending after A's error");

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
            int consume = Register(Yes2SDKIAP.Operation.ConsumePurchase, "consume");

            DeliverSuccess(Yes2SDKIAP.Operation.Purchase, consume, "wrong");
            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKIAP.PendingCountForTests, "a mismatched response must not remove the request");

            DeliverSuccess(Yes2SDKIAP.Operation.ConsumePurchase, consume, "");
            Assert.AreEqual(new[] { "consume:success:" }, _calls);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("[]")]
        [TestCase("|[]")]
        [TestCase("abc|[]")]
        [TestCase("-1|[]")]
        public void MessageWithoutARequestId_IsDropped(string message)
        {
            Register(Yes2SDKIAP.Operation.GetCatalog, "A");

            Yes2SDKIAP.HandleSuccessMessage(Yes2SDKIAP.Operation.GetCatalog, message);

            Assert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKIAP.PendingCountForTests);
        }

        [Test]
        public void PayloadContainingTheSeparator_IsDeliveredWhole()
        {
            int a = Register(Yes2SDKIAP.Operation.Purchase, "A");

            DeliverSuccess(Yes2SDKIAP.Operation.Purchase, a, "{\"payload\":\"x|y\"}");

            Assert.AreEqual(new[] { "A:success:{\"payload\":\"x|y\"}" }, _calls);
        }

        // --- #103: cleanup runs before game code ---

        [Test]
        public void StartingTheNextRequestFromASuccessCallback_KeepsItsCallbacks([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = 0;
            int a = Yes2SDKIAP.RegisterForTests(op,
                _ =>
                {
                    _calls.Add("A:success");
                    b = Register(op, "B");
                },
                _ => _calls.Add("A:error"));

            DeliverSuccess(op, a, "a");
            DeliverSuccess(op, b, "b");

            Assert.AreEqual(new[] { "A:success", "B:success:b" }, _calls);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }

        [Test]
        public void StartingTheNextRequestFromAnErrorCallback_KeepsItsCallbacks([ValueSource(nameof(AllOperations))] string operation)
        {
            var op = Op(operation);
            int b = 0;
            int a = Yes2SDKIAP.RegisterForTests(op,
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
            int a = Yes2SDKIAP.RegisterForTests(op,
                _ => throw new InvalidOperationException("game code failed"),
                _ => { });

            // The Bridge catches and logs this; here it reaches the test.
            Assert.Throws<InvalidOperationException>(() => DeliverSuccess(op, a, "a"));

            Assert.AreEqual(1, Yes2SDKIAP.PendingCountForTests, "A is removed even though its callback threw");
            DeliverSuccess(op, a, "again");
            DeliverSuccess(op, b, "b");
            Assert.AreEqual(new[] { "B:success:b" }, _calls);
        }

        [Test]
        public void SequentialConsumes_EachChainedFromThePreviousCallback_AllComplete()
        {
            // Purchase recovery: consume each pending purchase, starting the
            // next from the previous one's completion.
            var tokens = new[] { "t1", "t2", "t3" };
            var ids = new List<int>();

            void ConsumeNext(int index)
            {
                if (index >= tokens.Length) return;
                string token = tokens[index];
                ids.Add(Yes2SDKIAP.RegisterForTests(Yes2SDKIAP.Operation.ConsumePurchase,
                    _ =>
                    {
                        _calls.Add(token);
                        ConsumeNext(index + 1);
                    },
                    _ => _calls.Add(token + ":error")));
            }

            ConsumeNext(0);
            for (int i = 0; i < tokens.Length; i++)
            {
                DeliverSuccess(Yes2SDKIAP.Operation.ConsumePurchase, ids[i], "");
            }

            Assert.AreEqual(tokens, _calls);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }

        // --- Public API on the Editor path (synchronous, no popups headless) ---

        [Test]
        public void PublicApi_EachCallCompletesExactlyOnce()
        {
            int catalog = 0, purchases = 0, consume = 0, purchase = 0;

            Yes2SDK.IAP.GetCatalogAsync(_ => catalog++, _ => catalog++);
            Yes2SDK.IAP.GetPurchasesAsync(_ => purchases++, _ => purchases++);
            Yes2SDK.IAP.ConsumePurchaseAsync("token", () => consume++, _ => consume++);
            Yes2SDK.IAP.PurchaseAsync("product", _ => purchase++, _ => purchase++);

            Assert.AreEqual(1, catalog);
            Assert.AreEqual(1, purchases);
            Assert.AreEqual(1, consume);
            Assert.AreEqual(1, purchase);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }

        [Test]
        public void PublicApi_ConsumeStartedFromAConsumeCallback_Completes()
        {
            Yes2SDK.IAP.ConsumePurchaseAsync("a",
                () =>
                {
                    _calls.Add("a");
                    Yes2SDK.IAP.ConsumePurchaseAsync("b", () => _calls.Add("b"));
                });

            Assert.AreEqual(new[] { "a", "b" }, _calls);
            Assert.AreEqual(0, Yes2SDKIAP.PendingCountForTests);
        }
    }
}
