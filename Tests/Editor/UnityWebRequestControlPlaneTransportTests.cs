using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Yes2SDK.Tests
{
    public class UnityWebRequestControlPlaneTransportTests
    {
        private const string RefusedUrl = "http://127.0.0.1:1/bootstrap-v1.json";

        private static ControlPlaneTransportRequest Request()
        {
            return new ControlPlaneTransportRequest(RefusedUrl, TimeSpan.FromSeconds(5), ControlPlaneDocuments.MaxDocumentBytes, true);
        }

        private static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator GetAsync_ReportsItsOwnDeadlineAsTimeout()
        {
            var calls = 0;
            var transport = new UnityWebRequestControlPlaneTransport(() => calls++ == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(5));

            var task = transport.GetAsync(Request(), CancellationToken.None);
            yield return Wait(task);

            Assert.AreEqual(ControlPlaneTransportStatus.TimedOut, task.Result.Status);
        }

        [UnityTest]
        public IEnumerator GetAsync_ReportsCancelAbortAsCancellation()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var transport = new UnityWebRequestControlPlaneTransport(() =>
                {
                    cancellation.Cancel();
                    return TimeSpan.Zero;
                });

                var task = transport.GetAsync(Request(), cancellation.Token);
                yield return Wait(task);

                Assert.IsTrue(task.IsCanceled);
            }
        }

        [UnityTest]
        public IEnumerator GetAsync_ReportsRefusedConnectionAsNoResponse()
        {
            var transport = new UnityWebRequestControlPlaneTransport(() => TimeSpan.Zero);

            var task = transport.GetAsync(Request(), CancellationToken.None);
            yield return Wait(task);

            Assert.AreEqual(ControlPlaneTransportStatus.NetworkUnavailable, task.Result.Status);
            Assert.AreEqual(0, task.Result.HttpStatus);
        }
    }
}
