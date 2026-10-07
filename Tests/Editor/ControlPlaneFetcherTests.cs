using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    public class ControlPlaneFetcherTests
    {
        private static readonly byte[] SnapshotBytes = ControlPlaneFixture.Bytes(ControlPlaneFixture.Snapshot());
        private static readonly byte[] BootstrapBytes = ControlPlaneFixture.Bytes(ControlPlaneFixture.Bootstrap(SnapshotBytes));

        private FakeTransport _transport;
        private List<TimeSpan> _delays;
        private StubRandom _random;
        private CancellationTokenSource _cancellation;
        private Action _onDelay;
        private ControlPlaneFetcher _fetcher;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
            _delays = new List<TimeSpan>();
            _random = new StubRandom();
            _cancellation = new CancellationTokenSource();
            _onDelay = null;
            _fetcher = new ControlPlaneFetcher(_transport, Delay, _random);
        }

        [TearDown]
        public void TearDown()
        {
            _cancellation.Dispose();
        }

        private Task Delay(TimeSpan delay, CancellationToken token)
        {
            _delays.Add(delay);
            _onDelay?.Invoke();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private ControlPlaneResult<ControlPlaneBootstrap> FetchBootstrap()
        {
            return _fetcher.FetchBootstrapAsync(ControlPlaneFixture.BootstrapUrl, ControlPlaneFixture.Game, ControlPlaneFixture.Environment, _cancellation.Token).GetAwaiter().GetResult();
        }

        private ControlPlaneResult<ControlPlaneSnapshot> FetchSnapshot()
        {
            return _fetcher.FetchSnapshotAsync(ControlPlaneFixture.BootstrapFor(SnapshotBytes), _cancellation.Token).GetAwaiter().GetResult();
        }

        private static ControlPlaneTransportResponse Respond(string kind)
        {
            switch (kind)
            {
                case "network":
                    return ControlPlaneTransportResponse.NetworkUnavailable("offline");
                case "timeout":
                    return ControlPlaneTransportResponse.TimedOut();
                case "throw":
                    throw new IOException("socket closed");
                default:
                    return ControlPlaneTransportResponse.Completed(long.Parse(kind), null);
            }
        }

        [Test]
        public void FetchBootstrap_BypassesCacheWithFiveSecondAttempts()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, BootstrapBytes));

            var result = FetchBootstrap();

            Assert.IsTrue(result.Succeeded, result.Detail);
            var request = _transport.Requests[0];
            Assert.AreEqual(ControlPlaneFixture.BootstrapUrl, request.Url);
            Assert.IsTrue(request.BypassCache);
            Assert.AreEqual(TimeSpan.FromSeconds(5), request.Timeout);
            Assert.AreEqual(1048576, request.MaxBytes);
        }

        [Test]
        public void FetchSnapshot_UsesNormalCacheWithFiveSecondAttempts()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, SnapshotBytes));

            var result = FetchSnapshot();

            Assert.IsTrue(result.Succeeded, result.Detail);
            var request = _transport.Requests[0];
            Assert.AreEqual(ControlPlaneFixture.SnapshotUrl, request.Url);
            Assert.IsFalse(request.BypassCache);
            Assert.AreEqual(TimeSpan.FromSeconds(5), request.Timeout);
            Assert.AreEqual(1048576, request.MaxBytes);
        }

        [Test]
        public void Pipeline_FetchesSnapshotNamedByBootstrap()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, BootstrapBytes));
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, SnapshotBytes));

            var bootstrap = FetchBootstrap();
            var snapshot = _fetcher.FetchSnapshotAsync(bootstrap.Value, _cancellation.Token).GetAwaiter().GetResult();

            Assert.IsTrue(snapshot.Succeeded, snapshot.Detail);
            Assert.AreEqual(bootstrap.Value.SnapshotUrl, _transport.Requests[1].Url);
            Assert.AreEqual("rel-1", (string)snapshot.Value.Root["releaseId"]);
            CollectionAssert.AreEqual(SnapshotBytes, snapshot.Value.Bytes);
        }

        [TestCase("network", "network_unavailable")]
        [TestCase("timeout", "timeout")]
        [TestCase("throw", "network_unavailable")]
        [TestCase("408", "bootstrap_http_failed")]
        [TestCase("429", "bootstrap_http_failed")]
        [TestCase("500", "bootstrap_http_failed")]
        [TestCase("503", "bootstrap_http_failed")]
        [TestCase("599", "bootstrap_http_failed")]
        public void FetchBootstrap_RetriesOnceThenFails(string kind, string code)
        {
            _transport.Enqueue(() => Respond(kind));
            _transport.Enqueue(() => Respond(kind));

            var result = FetchBootstrap();

            Assert.AreEqual(code, ControlPlaneFixture.Code(result.Error));
            Assert.AreEqual(2, _transport.Requests.Count);
            Assert.AreEqual(1, _delays.Count);
        }

        [TestCase("network", "network_unavailable")]
        [TestCase("timeout", "timeout")]
        [TestCase("throw", "network_unavailable")]
        [TestCase("408", "snapshot_http_failed")]
        [TestCase("429", "snapshot_http_failed")]
        [TestCase("502", "snapshot_http_failed")]
        public void FetchSnapshot_RetriesOnceThenFails(string kind, string code)
        {
            _transport.Enqueue(() => Respond(kind));
            _transport.Enqueue(() => Respond(kind));

            var result = FetchSnapshot();

            Assert.AreEqual(code, ControlPlaneFixture.Code(result.Error));
            Assert.AreEqual(2, _transport.Requests.Count);
            Assert.AreEqual(1, _delays.Count);
        }

        [TestCase("timeout", "503", "bootstrap_http_failed")]
        [TestCase("503", "timeout", "timeout")]
        [TestCase("network", "429", "bootstrap_http_failed")]
        [TestCase("500", "network", "network_unavailable")]
        public void FetchBootstrap_ReportsLastAttemptAfterMixedFailures(string first, string second, string code)
        {
            _transport.Enqueue(() => Respond(first));
            _transport.Enqueue(() => Respond(second));

            Assert.AreEqual(code, ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(2, _transport.Requests.Count);
        }

        [Test]
        public void FetchBootstrap_DoesNotRetryOtherStatusAfterRetryableFailure()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(404, null));
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, BootstrapBytes));

            Assert.AreEqual("bootstrap_http_failed", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(2, _transport.Requests.Count);
        }

        [TestCase("400")]
        [TestCase("401")]
        [TestCase("403")]
        [TestCase("404")]
        [TestCase("410")]
        [TestCase("304")]
        [TestCase("199")]
        public void FetchBootstrap_DoesNotRetryOtherStatuses(string status)
        {
            _transport.Enqueue(() => Respond(status));

            var result = FetchBootstrap();

            Assert.AreEqual("bootstrap_http_failed", ControlPlaneFixture.Code(result.Error));
            StringAssert.Contains(status, result.Detail);
            Assert.AreEqual(1, _transport.Requests.Count);
            Assert.AreEqual(0, _delays.Count);
        }

        [TestCase("403")]
        [TestCase("404")]
        public void FetchSnapshot_DoesNotRetryOtherStatuses(string status)
        {
            _transport.Enqueue(() => Respond(status));

            Assert.AreEqual("snapshot_http_failed", ControlPlaneFixture.Code(FetchSnapshot().Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [TestCase("network")]
        [TestCase("timeout")]
        [TestCase("503")]
        public void FetchBootstrap_RecoversOnSecondAttempt(string kind)
        {
            _transport.Enqueue(() => Respond(kind));
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, BootstrapBytes));

            var result = FetchBootstrap();

            Assert.IsTrue(result.Succeeded, result.Detail);
            Assert.AreEqual(2, _transport.Requests.Count);
        }

        [Test]
        public void FetchSnapshot_RecoversOnSecondAttempt()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, SnapshotBytes));

            Assert.IsTrue(FetchSnapshot().Succeeded);
            Assert.AreEqual(2, _transport.Requests.Count);
        }

        [TestCase(true, 250)]
        [TestCase(false, 500)]
        public void Retry_WaitsFrom250To500MillisecondsInclusive(bool lowest, int expected)
        {
            _random.Lowest = lowest;
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());

            FetchBootstrap();

            Assert.AreEqual(250, _random.MinValue);
            Assert.AreEqual(501, _random.MaxValue);
            CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(expected) }, _delays);
        }

        [Test]
        public void Retry_DrawsFreshJitterPerFetch()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());
            _transport.Enqueue(ControlPlaneTransportResponse.TimedOut());

            FetchBootstrap();
            FetchBootstrap();

            Assert.AreEqual(2, _random.Calls);
        }

        [Test]
        public void FetchBootstrap_DoesNotRetryInvalidBody()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, new byte[] { 0x7b }));

            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test]
        public void FetchBootstrap_TreatsEmptySuccessAsInvalid()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(204, null));

            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(FetchBootstrap().Error));
        }

        [Test]
        public void FetchBootstrap_DoesNotRetryUnsupportedProtocol()
        {
            var bootstrap = ControlPlaneFixture.Bootstrap(SnapshotBytes);
            bootstrap["protocolVersion"] = 2;
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, ControlPlaneFixture.Bytes(bootstrap)));

            Assert.AreEqual("protocol_unsupported", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test]
        public void FetchBootstrap_RejectsOtherEnvironment()
        {
            var bootstrap = ControlPlaneFixture.Bootstrap(SnapshotBytes);
            bootstrap["environmentKey"] = "staging";
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, ControlPlaneFixture.Bytes(bootstrap)));

            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(FetchBootstrap().Error));
        }

        [Test]
        public void FetchBootstrap_DoesNotRetryOversizeSuccess()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.TooLarge(200));

            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test]
        public void FetchBootstrap_ReportsOversizeErrorPageAsHttpFailure()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.TooLarge(503));
            _transport.Enqueue(ControlPlaneTransportResponse.TooLarge(503));

            Assert.AreEqual("bootstrap_http_failed", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(2, _transport.Requests.Count);
        }

        [Test]
        public void FetchSnapshot_DoesNotRetryOversizeSuccess()
        {
            _transport.Enqueue(ControlPlaneTransportResponse.TooLarge(200));

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(FetchSnapshot().Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test]
        public void FetchSnapshot_RejectsBodyOverOneMebibyteFromTransport()
        {
            var oversize = new byte[ControlPlaneDocuments.MaxDocumentBytes + 1];
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, oversize));

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(FetchSnapshot().Error));
        }

        [Test]
        public void FetchSnapshot_DoesNotRetryDigestMismatch()
        {
            var tampered = ControlPlaneFixture.Snapshot();
            tampered["resources"][0]["data"]["speed"] = 9;
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, ControlPlaneFixture.Bytes(tampered)));

            var result = FetchSnapshot();

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(result.Error));
            StringAssert.Contains("digest", result.Detail);
            Assert.AreEqual(1, _transport.Requests.Count);
            Assert.AreEqual(0, _delays.Count);
        }

        [Test]
        public void FetchSnapshot_ChecksDigestBeforeProtocol()
        {
            var unsupported = ControlPlaneFixture.Snapshot();
            unsupported["protocolVersion"] = 2;
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, ControlPlaneFixture.Bytes(unsupported)));

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(FetchSnapshot().Error));
        }

        [Test]
        public void FetchSnapshot_ReportsUnsupportedProtocolWhenDigestMatches()
        {
            var unsupported = ControlPlaneFixture.Snapshot();
            unsupported["protocolVersion"] = 2;
            var bytes = ControlPlaneFixture.Bytes(unsupported);
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, bytes));

            var result = _fetcher.FetchSnapshotAsync(ControlPlaneFixture.BootstrapFor(bytes), _cancellation.Token).GetAwaiter().GetResult();

            Assert.AreEqual("protocol_unsupported", ControlPlaneFixture.Code(result.Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test]
        public void FetchSnapshot_RejectsOversizeArtifactDescriptor()
        {
            var snapshot = ControlPlaneFixture.Snapshot();
            snapshot["artifacts"][0]["size"] = ControlPlaneDocuments.MaxArtifactBytes + 1;
            var bytes = ControlPlaneFixture.Bytes(snapshot);
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, bytes));

            var result = _fetcher.FetchSnapshotAsync(ControlPlaneFixture.BootstrapFor(bytes), _cancellation.Token).GetAwaiter().GetResult();

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(result.Error));
        }

        [Test]
        public void Fetch_ReturnsCancelledWithoutRequestWhenAlreadyCancelled()
        {
            _cancellation.Cancel();

            Assert.AreEqual("cancelled", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(0, _transport.Requests.Count);
        }

        [Test]
        public void Fetch_ReturnsCancelledWhenTransportIsCancelled()
        {
            _transport.Enqueue(() =>
            {
                _cancellation.Cancel();
                throw new OperationCanceledException(_cancellation.Token);
            });

            Assert.AreEqual("cancelled", ControlPlaneFixture.Code(FetchSnapshot().Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test]
        public void Fetch_ReturnsCancelledWhenCancelledAfterResponse()
        {
            _transport.Enqueue(() =>
            {
                _cancellation.Cancel();
                return ControlPlaneTransportResponse.Completed(200, BootstrapBytes);
            });

            Assert.AreEqual("cancelled", ControlPlaneFixture.Code(FetchBootstrap().Error));
        }

        [Test]
        public void Fetch_ReturnsCancelledDuringRetryDelay()
        {
            _onDelay = _cancellation.Cancel;
            _transport.Enqueue(ControlPlaneTransportResponse.NetworkUnavailable("offline"));
            _transport.Enqueue(ControlPlaneTransportResponse.Completed(200, BootstrapBytes));

            Assert.AreEqual("cancelled", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test]
        public void Fetch_TreatsForeignCancellationAsNoResponse()
        {
            _transport.Enqueue(() => throw new OperationCanceledException());
            _transport.Enqueue(() => throw new TaskCanceledException());

            Assert.AreEqual("network_unavailable", ControlPlaneFixture.Code(FetchBootstrap().Error));
            Assert.AreEqual(2, _transport.Requests.Count);
        }

        [Test]
        public void Fetch_NeverThrowsTransportExceptions()
        {
            _transport.Enqueue(() => throw new InvalidOperationException("boom"));
            _transport.Enqueue(() => throw new InvalidOperationException("boom"));

            ControlPlaneResult<ControlPlaneSnapshot> result = null;
            Assert.DoesNotThrow(() => result = FetchSnapshot());
            Assert.AreEqual("network_unavailable", ControlPlaneFixture.Code(result.Error));
            StringAssert.Contains("boom", result.Detail);
        }

        private sealed class FakeTransport : IControlPlaneTransport
        {
            private readonly Queue<Func<ControlPlaneTransportResponse>> _responses = new Queue<Func<ControlPlaneTransportResponse>>();

            public List<ControlPlaneTransportRequest> Requests { get; } = new List<ControlPlaneTransportRequest>();

            public void Enqueue(ControlPlaneTransportResponse response)
            {
                _responses.Enqueue(() => response);
            }

            public void Enqueue(Func<ControlPlaneTransportResponse> response)
            {
                _responses.Enqueue(response);
            }

            public Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
            {
                Requests.Add(request);
                try
                {
                    return Task.FromResult(_responses.Dequeue()());
                }
                catch (Exception e)
                {
                    var failed = new TaskCompletionSource<ControlPlaneTransportResponse>();
                    if (e is OperationCanceledException)
                    {
                        failed.SetCanceled();
                    }
                    else
                    {
                        failed.SetException(e);
                    }
                    return failed.Task;
                }
            }
        }

        private sealed class StubRandom : Random
        {
            public bool Lowest = true;
            public int MinValue;
            public int MaxValue;
            public int Calls;

            public override int Next(int minValue, int maxValue)
            {
                Calls++;
                MinValue = minValue;
                MaxValue = maxValue;
                return Lowest ? minValue : maxValue - 1;
            }
        }
    }
}
