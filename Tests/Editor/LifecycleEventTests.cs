using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the exit request lifecycle event: the bridge message raises
    /// Yes2SDK.OnExitRequested once, and a throwing handler is logged instead
    /// of escaping into the platform callback.
    /// </summary>
    public class LifecycleEventTests
    {
        private int _raised;

        private void CountRaise() { _raised++; }
        private static void Throw() { throw new InvalidOperationException("exit handler failed"); }

        [SetUp]
        public void SetUp()
        {
            _raised = 0;
            Yes2SDK.OnExitRequested += CountRaise;
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDK.OnExitRequested -= CountRaise;
            Yes2SDK.OnExitRequested -= Throw;
        }

        [Test]
        public void OnExitRequested_BridgeMessage_RaisesEventOnce()
        {
            Bridge.Instance.OnExitRequested("");

            Assert.AreEqual(1, _raised);
        }

        [Test]
        public void OnExitRequested_ThrowingHandler_IsLoggedAndDoesNotPropagate()
        {
            Yes2SDK.OnExitRequested += Throw;
            LogAssert.Expect(LogType.Error, new Regex("callback 'OnExitRequested' threw"));

            Assert.DoesNotThrow(() => Bridge.Instance.OnExitRequested(""));
        }

        [Test]
        public void RaiseExitRequestAndFlush_ThrowingHandler_IsLoggedAndDoesNotPropagate()
        {
            Yes2SDK.OnExitRequested += Throw;
            LogAssert.Expect(LogType.Error, new Regex("OnExitRequested handler threw"));

            Assert.DoesNotThrow(() => Yes2SDKEditorMock.RaiseExitRequestAndFlush());
        }
    }
}
