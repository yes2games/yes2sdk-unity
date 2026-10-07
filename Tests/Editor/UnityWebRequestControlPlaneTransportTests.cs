using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Yes2SDK.Tests
{
    public class UnityWebRequestControlPlaneTransportTests
    {
        private TcpListener _silent;

        [SetUp]
        public void SetUp()
        {
            _silent = new TcpListener(IPAddress.Loopback, 0);
            _silent.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _silent.Stop();
        }

        private ControlPlaneTransportRequest SilentRequest()
        {
            return Request("http://127.0.0.1:" + ((IPEndPoint)_silent.LocalEndpoint).Port + "/bootstrap-v1.json");
        }

        private static ControlPlaneTransportRequest Request(string url)
        {
            return new ControlPlaneTransportRequest(url, TimeSpan.FromSeconds(5), ControlPlaneDocuments.MaxDocumentBytes, true);
        }

        private static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator GetAsync_AbortsAtItsOwnDeadlineAsTimeout()
        {
            var calls = 0;
            var transport = new UnityWebRequestControlPlaneTransport(() => calls++ == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(5));

            var task = transport.GetAsync(SilentRequest(), CancellationToken.None);
            yield return Wait(task);

            Assert.AreEqual(ControlPlaneTransportStatus.TimedOut, task.Result.Status);
        }

        [UnityTest]
        public IEnumerator GetAsync_AbortsOnCancelAsCancellation()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var transport = new UnityWebRequestControlPlaneTransport(() =>
                {
                    cancellation.Cancel();
                    return TimeSpan.Zero;
                });

                var task = transport.GetAsync(SilentRequest(), cancellation.Token);
                yield return Wait(task);

                Assert.IsTrue(task.IsCanceled);
            }
        }

        [UnityTest]
        public IEnumerator GetAsync_ReportsRefusedConnectionAsNoResponse()
        {
            var transport = new UnityWebRequestControlPlaneTransport(() => TimeSpan.Zero);

            var task = transport.GetAsync(Request("http://127.0.0.1:1/bootstrap-v1.json"), CancellationToken.None);
            yield return Wait(task);

            Assert.AreEqual(ControlPlaneTransportStatus.NetworkUnavailable, task.Result.Status);
            Assert.AreEqual(0, task.Result.HttpStatus);
        }
    }
}
