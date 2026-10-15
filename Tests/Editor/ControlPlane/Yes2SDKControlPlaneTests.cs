using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Yes2SDK.Tests
{
    public class Yes2SDKControlPlaneTests
    {
        private static readonly byte[] Snapshot1 = ControlPlaneFixture.SnapshotBytesFor("rel-1");
        private static readonly byte[] Bootstrap1 = ControlPlaneFixture.BootstrapBytesFor("dep-1", "rel-1", Snapshot1);
        private static readonly byte[] Snapshot2 = ControlPlaneFixture.SnapshotBytesFor("rel-2");
        private static readonly byte[] Bootstrap2 = ControlPlaneFixture.BootstrapBytesFor("dep-2", "rel-2", Snapshot2);

        private string _root;
        private RoutingTransport _transport;
        private ManualTimer _timer;
        private Yes2SDKControlPlane _controlPlane;
        private List<ControlPlaneActivation> _activations;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _root = Path.Combine(Path.GetTempPath(), "yes2sdk-cp-" + Guid.NewGuid().ToString("N"));
            _transport = new RoutingTransport();
            _timer = new ManualTimer();
            _controlPlane = NewControlPlane();
        }

        [TearDown]
        public void TearDown()
        {
            _controlPlane.Shutdown();
            SynchronizationContext.SetSynchronizationContext(_context);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private Yes2SDKControlPlane NewControlPlane()
        {
            var controlPlane = new Yes2SDKControlPlane(
                _transport,
                _timer.Delay,
                new System.Random(0),
                (game, environment) => new ControlPlaneLkgJournal(JournalDirectory(game, environment), game, environment));
            _activations = new List<ControlPlaneActivation>();
            controlPlane.DeploymentActivated += _activations.Add;
            return controlPlane;
        }

        private string JournalDirectory(string game, string environment)
        {
            return Path.Combine(_root, game, environment);
        }

        private ControlPlaneLkgJournal Journal()
        {
            return new ControlPlaneLkgJournal(JournalDirectory(ControlPlaneFixture.Game, ControlPlaneFixture.Environment), ControlPlaneFixture.Game, ControlPlaneFixture.Environment);
        }

        private static ControlPlaneOptions Options()
        {
            return new ControlPlaneOptions { GameKey = ControlPlaneFixture.Game, EnvironmentKey = ControlPlaneFixture.Environment };
        }

        private ControlPlaneStatus Initialize(CancellationToken token = default)
        {
            return _controlPlane.InitializeAsync(Options(), token).GetAwaiter().GetResult();
        }

        private ControlPlaneStatus Refresh()
        {
            return _controlPlane.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private int BootstrapRequests => _transport.Requests.Count(url => url == ControlPlaneFixture.BootstrapUrl);

        private void Serve(byte[] bootstrap, byte[] snapshot)
        {
            _transport.Respond(ControlPlaneFixture.BootstrapUrl, ControlPlaneTransportResponse.Completed(200, bootstrap));
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, snapshot));
        }

        private void ServeBootstrap(ControlPlaneTransportResponse response)
        {
            _transport.Respond(ControlPlaneFixture.BootstrapUrl, response);
        }

        private static void AssertActive(ControlPlaneStatus status, ControlPlaneSource source, string deploymentId, string releaseId, string lastError)
        {
            Assert.IsTrue(status.Available);
            Assert.AreEqual(source, status.Source);
            Assert.AreEqual(ControlPlaneFixture.Game, status.GameKey);
            Assert.AreEqual(ControlPlaneFixture.Environment, status.EnvironmentKey);
            Assert.AreEqual(deploymentId, status.DeploymentId);
            Assert.AreEqual(releaseId, status.ReleaseId);
            Assert.AreEqual(1, status.ProtocolVersion);
            Assert.AreEqual(lastError, status.LastError);
        }

        private void AssertActivations(params string[] deploymentIds)
        {
            CollectionAssert.AreEqual(deploymentIds, _activations.Select(a => a.DeploymentId).ToArray());
        }

        [Test]
        public void Status_IsUnavailableBeforeInitialize()
        {
            var status = _controlPlane.Status;

            Assert.IsFalse(status.Available);
            Assert.AreEqual(ControlPlaneSource.None, status.Source);
            Assert.IsNull(status.ProtocolVersion);
            Assert.IsFalse(_controlPlane.TryGetResource("runtime.config", out _));
        }

        [Test]
        public void Initialize_WithoutLkgActivatesRemoteAndPersistsIt()
        {
            Serve(Bootstrap1, Snapshot1);

            var status = Initialize();

            AssertActive(status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            AssertActive(_controlPlane.Status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            CollectionAssert.AreEqual(new[] { ControlPlaneFixture.BootstrapUrl, ControlPlaneFixture.SnapshotUrl }, _transport.Requests);
            Assert.AreEqual("dep-1", Journal().Load().Bootstrap.DeploymentId);
            AssertActivations("dep-1");
            Assert.AreEqual(ControlPlaneSource.Remote, _activations[0].Source);
        }

        [Test]
        public void Initialize_ActivatesLkgBeforeRemoteCompletes()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);

            var initializing = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            Assert.IsFalse(initializing.IsCompleted);
            AssertActive(_controlPlane.Status, ControlPlaneSource.Lkg, "dep-1", "rel-1", null);
            Assert.IsTrue(_controlPlane.TryGetResource("runtime.config", out _));
            AssertActivations("dep-1");
            Assert.AreEqual(ControlPlaneSource.Lkg, _activations[0].Source);

            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
            AssertActive(initializing.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Initialize_SameRemoteDeploymentAsLkgConfirmsWithoutSnapshotFetch()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            ServeBootstrap(ControlPlaneTransportResponse.Completed(200, Bootstrap1));

            var status = Initialize();

            AssertActive(status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            CollectionAssert.AreEqual(new[] { ControlPlaneFixture.BootstrapUrl }, _transport.Requests);
            AssertActivations("dep-1");
        }

        [Test]
        public void Initialize_NewRemoteDeploymentReplacesLkg()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            Serve(Bootstrap2, Snapshot2);

            var status = Initialize();

            AssertActive(status, ControlPlaneSource.Remote, "dep-2", "rel-2", null);
            AssertActivations("dep-1", "dep-2");
            Assert.AreEqual("dep-2", Journal().Load().Bootstrap.DeploymentId);
        }

        [TestCase("network", "network_unavailable")]
        [TestCase("timeout", "timeout")]
        [TestCase("404", "bootstrap_http_failed")]
        [TestCase("500", "bootstrap_http_failed")]
        public void Initialize_RemoteFailureKeepsLkg(string kind, string code)
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            ServeBootstrap(Failure(kind));

            var status = Initialize();

            AssertActive(status, ControlPlaneSource.Lkg, "dep-1", "rel-1", code);
            Assert.IsTrue(_controlPlane.TryGetResource("runtime.config", out _));
            AssertActivations("dep-1");
        }

        [TestCase("network", "network_unavailable")]
        [TestCase("404", "bootstrap_http_failed")]
        public void Initialize_RemoteFailureWithoutLkgIsUnavailable(string kind, string code)
        {
            ServeBootstrap(Failure(kind));

            var status = Initialize();

            Assert.IsFalse(status.Available);
            Assert.AreEqual(ControlPlaneSource.None, status.Source);
            Assert.AreEqual(ControlPlaneFixture.Game, status.GameKey);
            Assert.IsNull(status.DeploymentId);
            Assert.AreEqual(code, status.LastError);
            Assert.IsFalse(_controlPlane.TryGetResource("runtime.config", out _));
            AssertActivations();
        }

        [Test]
        public void Initialize_SnapshotDigestMismatchKeepsLkg()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            Serve(Bootstrap2, Snapshot1);

            var status = Initialize();

            AssertActive(status, ControlPlaneSource.Lkg, "dep-1", "rel-1", "snapshot_invalid");
            Assert.AreEqual("dep-1", Journal().Load().Bootstrap.DeploymentId);
            AssertActivations("dep-1");
        }

        [Test]
        public void Initialize_UnsupportedProtocolKeepsLkg()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            var bootstrap = JObject.Parse(System.Text.Encoding.UTF8.GetString(Bootstrap2));
            bootstrap["protocolVersion"] = 2;
            ServeBootstrap(ControlPlaneTransportResponse.Completed(200, ControlPlaneFixture.Bytes(bootstrap)));

            AssertActive(Initialize(), ControlPlaneSource.Lkg, "dep-1", "rel-1", "protocol_unsupported");
        }

        [Test]
        public void Initialize_CorruptLkgSlotRecoversPreviousDeployment()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            Journal().Write(ControlPlaneFixture.Deployment("dep-2", "rel-2"));
            File.WriteAllText(Path.Combine(JournalDirectory(ControlPlaneFixture.Game, ControlPlaneFixture.Environment), ControlPlaneLkgJournal.SlotB), "torn");
            ServeBootstrap(Failure("network"));

            AssertActive(Initialize(), ControlPlaneSource.Lkg, "dep-1", "rel-1", "network_unavailable");
        }

        [Test]
        public void Initialize_PersistenceUnavailableStillActivatesRemote()
        {
            Directory.CreateDirectory(Path.Combine(_root, ControlPlaneFixture.Game));
            File.WriteAllText(JournalDirectory(ControlPlaneFixture.Game, ControlPlaneFixture.Environment), "not a directory");
            Serve(Bootstrap1, Snapshot1);
            LogAssert.Expect(LogType.Warning, new Regex("ControlPlane cache unavailable"));

            var status = Initialize();

            AssertActive(status, ControlPlaneSource.Remote, "dep-1", "rel-1", "cache_unavailable");
            Assert.IsTrue(_controlPlane.TryGetResource("runtime.config", out _));
            AssertActivations("dep-1");
        }

        [Test]
        public void Initialize_OfflineRestartActivatesPersistedDeployment()
        {
            Serve(Bootstrap2, Snapshot2);
            Initialize();
            _controlPlane.Shutdown();

            _transport = new RoutingTransport();
            ServeBootstrap(Failure("network"));
            _controlPlane = NewControlPlane();

            AssertActive(Initialize(), ControlPlaneSource.Lkg, "dep-2", "rel-2", "network_unavailable");
            AssertActivations("dep-2");
        }

        [Test]
        public void Initialize_CancelledTokenKeepsLkgAndReportsCancelled()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            Serve(Bootstrap2, Snapshot2);

            var status = Initialize(new CancellationToken(true));

            AssertActive(status, ControlPlaneSource.Lkg, "dep-1", "rel-1", "cancelled");
            AssertActivations("dep-1");
            Assert.IsEmpty(_transport.Requests);
        }

        [Test]
        public void Initialize_AfterPreCancelledFirstCallFetchesRemote()
        {
            Serve(Bootstrap1, Snapshot1);

            Assert.AreEqual("cancelled", Initialize(new CancellationToken(true)).LastError);
            Assert.IsEmpty(_transport.Requests);

            AssertActive(Initialize(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            AssertActivations("dep-1");
        }

        [Test]
        public void Initialize_ReentrantInitializeFromActivationHandlerReturnsCurrentStatus()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            Task<ControlPlaneStatus> reentrant = null;
            _controlPlane.DeploymentActivated += _ => reentrant ??= _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            var initializing = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            Assert.IsTrue(reentrant.IsCompleted);
            AssertActive(reentrant.Result, ControlPlaneSource.Lkg, "dep-1", "rel-1", null);
            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
            AssertActive(initializing.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Initialize_UsesNormalizedRuntimeOrigin()
        {
            var options = Options();
            options.RuntimeOrigin = "https://Runtime.Example.COM:443/";
            _transport.Respond("https://runtime.example.com/environments/nsr/production/bootstrap-v1.json", ControlPlaneTransportResponse.Completed(200, Bootstrap1));
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, Snapshot1));

            var status = _controlPlane.InitializeAsync(options, CancellationToken.None).GetAwaiter().GetResult();

            AssertActive(status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Initialize_SameOptionsAgainIsIdempotent()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            var options = Options();
            options.RuntimeOrigin = "https://runtime.yes2games.com/";

            var status = _controlPlane.InitializeAsync(options, CancellationToken.None).GetAwaiter().GetResult();

            AssertActive(status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            Assert.AreEqual(2, _transport.Requests.Count);
            AssertActivations("dep-1");
        }

        [Test]
        public void Initialize_ConcurrentSameOptionsShareOneInitialization()
        {
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);

            var first = _controlPlane.InitializeAsync(Options(), CancellationToken.None);
            var second = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            Assert.AreEqual(1, _transport.Requests.Count);
            pending.SetResult(ControlPlaneTransportResponse.NetworkUnavailable("offline"));
            Assert.AreEqual("network_unavailable", first.GetAwaiter().GetResult().LastError);
            Assert.AreEqual("network_unavailable", second.GetAwaiter().GetResult().LastError);
            Assert.AreEqual(2, _transport.Requests.Count);
        }

        [Test]
        public void Initialize_CoalescedInitializeIsNotCancelledByFirstCallersToken()
        {
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, Snapshot1));
            using (var cancellation = new CancellationTokenSource())
            {
                var first = _controlPlane.InitializeAsync(Options(), cancellation.Token);
                var second = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

                cancellation.Cancel();

                Assert.AreEqual("cancelled", first.GetAwaiter().GetResult().LastError);
                Assert.IsFalse(second.IsCompleted);
                pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
                AssertActive(second.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            }
        }

        [Test]
        public void Initialize_JoinerTokenCancelsOnlyItsOwnWait()
        {
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, Snapshot1));
            using (var cancellation = new CancellationTokenSource())
            {
                var first = _controlPlane.InitializeAsync(Options(), CancellationToken.None);
                var joiner = _controlPlane.InitializeAsync(Options(), cancellation.Token);

                cancellation.Cancel();

                Assert.IsTrue(joiner.IsCompleted);
                Assert.AreEqual("cancelled", joiner.Result.LastError);
                Assert.IsFalse(first.IsCompleted);
                pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
                AssertActive(first.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            }
        }

        [Test]
        public void Initialize_LateInitializeAfterFirstCallerCancelledWaitsForRefresh()
        {
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, Snapshot1));
            using (var cancellation = new CancellationTokenSource())
            {
                var first = _controlPlane.InitializeAsync(Options(), cancellation.Token);
                cancellation.Cancel();
                Assert.AreEqual("cancelled", first.GetAwaiter().GetResult().LastError);
            }

            var late = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            Assert.IsFalse(late.IsCompleted);
            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
            AssertActive(late.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Initialize_DifferentOptionsRequireShutdown()
        {
            ServeBootstrap(Failure("network"));
            Initialize();
            var other = Options();
            other.EnvironmentKey = "staging";

            Assert.Throws<InvalidOperationException>(() => _controlPlane.InitializeAsync(other, CancellationToken.None));
            Assert.Throws<InvalidOperationException>(() => _controlPlane.InitializeAsync(new ControlPlaneOptions { GameKey = "nsr", EnvironmentKey = "production", RefreshIntervalSeconds = 0 }, CancellationToken.None));

            _controlPlane.Shutdown();
            var status = _controlPlane.InitializeAsync(other, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("staging", status.EnvironmentKey);
        }

        [TestCase(null, "production", "https://runtime.yes2games.com", 60)]
        [TestCase("NSR", "production", "https://runtime.yes2games.com", 60)]
        [TestCase("-nsr", "production", "https://runtime.yes2games.com", 60)]
        [TestCase("nsr", "", "https://runtime.yes2games.com", 60)]
        [TestCase("nsr", ".production", "https://runtime.yes2games.com", 60)]
        [TestCase("nsr", "production", null, 60)]
        [TestCase("nsr", "production", "http://runtime.yes2games.com", 60)]
        [TestCase("nsr", "production", "https://runtime.yes2games.com/base", 60)]
        [TestCase("nsr", "production", "https://runtime.yes2games.com/?q=1", 60)]
        [TestCase("nsr", "production", "https://user@runtime.yes2games.com", 60)]
        [TestCase("nsr", "production", "runtime.yes2games.com", 60)]
        [TestCase("nsr", "production", "https://runtime.yes2games.com", 14)]
        [TestCase("nsr", "production", "https://runtime.yes2games.com", 3601)]
        [TestCase("nsr", "production", "https://runtime.yes2games.com", -1)]
        public void Initialize_RejectsInvalidOptions(string game, string environment, string origin, int interval)
        {
            var options = new ControlPlaneOptions { GameKey = game, EnvironmentKey = environment, RuntimeOrigin = origin, RefreshIntervalSeconds = interval };

            Assert.Throws<ArgumentException>(() => _controlPlane.InitializeAsync(options, CancellationToken.None));
            Assert.Throws<ArgumentNullException>(() => _controlPlane.InitializeAsync(null, CancellationToken.None));
            Assert.IsEmpty(_transport.Requests);
        }

        [TestCase(0)]
        [TestCase(15)]
        [TestCase(3600)]
        public void Initialize_AcceptsRefreshIntervalBounds(int interval)
        {
            ServeBootstrap(Failure("network"));
            var options = Options();
            options.RefreshIntervalSeconds = interval;

            Assert.AreEqual("network_unavailable", _controlPlane.InitializeAsync(options, CancellationToken.None).GetAwaiter().GetResult().LastError);
        }

        [Test]
        public void Options_DefaultToProductionOriginAndSixtySeconds()
        {
            var options = new ControlPlaneOptions();

            Assert.AreEqual("https://runtime.yes2games.com", options.RuntimeOrigin);
            Assert.AreEqual(60, options.RefreshIntervalSeconds);
        }

        [Test]
        public void Refresh_BeforeInitializeThrows()
        {
            Assert.Throws<InvalidOperationException>(() => _controlPlane.RefreshAsync(CancellationToken.None));
        }

        [Test]
        public void Refresh_SameDeploymentFetchesNoSnapshotAndRaisesNoEvent()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            AssertActive(Refresh(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            AssertActive(Refresh(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);

            Assert.AreEqual(1, _transport.Requests.Count(url => url == ControlPlaneFixture.SnapshotUrl));
            AssertActivations("dep-1");
        }

        [Test]
        public void Refresh_RaisesOneEventPerDistinctDeployment()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            Refresh();
            Serve(Bootstrap2, Snapshot2);
            Refresh();
            Refresh();
            Serve(Bootstrap1, Snapshot1);
            Refresh();

            AssertActivations("dep-1", "dep-2", "dep-1");
            Assert.AreEqual("dep-1", Journal().Load().Bootstrap.DeploymentId);
        }

        [Test]
        public void Refresh_FailureAfterRemoteKeepsActiveDeployment()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            ServeBootstrap(Failure("500"));

            AssertActive(Refresh(), ControlPlaneSource.Remote, "dep-1", "rel-1", "bootstrap_http_failed");
            Serve(Bootstrap1, Snapshot1);
            AssertActive(Refresh(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Refresh_ConcurrentCallsShareOneRefresh()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);

            var first = _controlPlane.RefreshAsync(CancellationToken.None);
            var second = _controlPlane.RefreshAsync(CancellationToken.None);

            Assert.AreEqual(3, _transport.Requests.Count);
            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
            AssertActive(first.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            AssertActive(second.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            Refresh();
            Assert.AreEqual(4, _transport.Requests.Count);
        }

        [Test]
        public void Refresh_CoalescedCallerIsNotCancelledByAnotherCallersToken()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, Snapshot2));
            using (var cancellation = new CancellationTokenSource())
            {
                var cancelledCaller = _controlPlane.RefreshAsync(cancellation.Token);
                var otherCaller = _controlPlane.RefreshAsync(CancellationToken.None);

                cancellation.Cancel();

                AssertActive(cancelledCaller.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", "cancelled");
                Assert.IsFalse(otherCaller.IsCompleted);
                Assert.IsFalse(pending.Task.IsCompleted);
                pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap2));
                AssertActive(otherCaller.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-2", "rel-2", null);
            }
            Assert.AreEqual(2, _transport.Requests.Count(url => url == ControlPlaneFixture.BootstrapUrl));
            AssertActivations("dep-1", "dep-2");
        }

        [Test]
        public void Refresh_CallerCancellationKeepsActiveDeployment()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            using (var cancellation = new CancellationTokenSource())
            {
                var refresh = _controlPlane.RefreshAsync(cancellation.Token);
                cancellation.Cancel();

                AssertActive(refresh.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", "cancelled");
            }
            AssertActive(_controlPlane.Status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            Assert.IsFalse(pending.Task.IsCompleted);
            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
            AssertActive(_controlPlane.Status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Shutdown_CancelsOutstandingRequestsAndForgetsState()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            var initializing = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            _controlPlane.Shutdown();

            Assert.IsTrue(pending.Task.IsCanceled);
            var status = initializing.GetAwaiter().GetResult();
            Assert.IsFalse(status.Available);
            Assert.IsNull(status.GameKey);
            Assert.IsFalse(_controlPlane.Status.Available);
            Assert.IsFalse(_controlPlane.TryGetResource("runtime.config", out _));
            Assert.Throws<InvalidOperationException>(() => _controlPlane.RefreshAsync(CancellationToken.None));
        }

        [Test]
        public void Shutdown_DuringSnapshotFetchNeverActivatesOrPersists()
        {
            ServeBootstrap(ControlPlaneTransportResponse.Completed(200, Bootstrap1));
            var pending = _transport.Hold(ControlPlaneFixture.SnapshotUrl);
            var initializing = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            _controlPlane.Shutdown();
            initializing.GetAwaiter().GetResult();

            Assert.IsTrue(pending.Task.IsCanceled);
            Assert.IsNull(Journal().Load());
            AssertActivations();
        }

        [Test]
        public void Shutdown_StaleWaiterGetsCancelledNotTheNewSessionsStatus()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl, false);
            var stale = _controlPlane.InitializeAsync(Options(), CancellationToken.None);

            _controlPlane.Shutdown();
            var options = Options();
            options.RefreshIntervalSeconds = 30;
            var current = _controlPlane.InitializeAsync(options, CancellationToken.None);
            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap1));

            var status = stale.GetAwaiter().GetResult();
            Assert.IsFalse(status.Available);
            Assert.AreEqual(ControlPlaneSource.None, status.Source);
            Assert.IsNull(status.GameKey);
            Assert.IsNull(status.DeploymentId);
            Assert.AreEqual("cancelled", status.LastError);
            AssertActive(current.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Refresh_NextWriteTargetsTheSlotOppositeTheLastCommittedOne()
        {
            Journal().Write(ControlPlaneFixture.Deployment("dep-1", "rel-1"));
            Serve(Bootstrap2, Snapshot2);
            Initialize();
            Serve(Bootstrap1, Snapshot1);

            Refresh();

            Assert.AreEqual("lkg-a.dat 3", File.ReadAllText(Path.Combine(JournalDirectory(ControlPlaneFixture.Game, ControlPlaneFixture.Environment), ControlPlaneLkgJournal.Pointer)));
            Assert.AreEqual("dep-1", Journal().Load().Bootstrap.DeploymentId);
        }

        [Test]
        public void Shutdown_ThenInitializeAnnouncesDeploymentAgain()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            _controlPlane.Shutdown();

            Initialize();

            AssertActivations("dep-1", "dep-1");
        }

        [Test]
        public void Reset_ShutsDownAndDropsSubscribers()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            _controlPlane.Reset();

            Assert.IsFalse(_controlPlane.Status.Available);
            Initialize();
            AssertActivations("dep-1");
        }

        [Test]
        public void Entry_ResetsOnEveryPlayModeEntryInTheEditor()
        {
            var reset = typeof(Yes2ControlPlane).GetMethod("ResetEditorState", BindingFlags.Static | BindingFlags.NonPublic);
            var attribute = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

            var subscribers = typeof(Yes2SDKControlPlane).GetField("DeploymentActivated", BindingFlags.Instance | BindingFlags.NonPublic);
            Yes2ControlPlane.Instance.DeploymentActivated += _activations.Add;
            Assert.IsNotNull(subscribers.GetValue(Yes2ControlPlane.Instance));

            Yes2ControlPlane.ResetEditorState();

            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);
            Assert.IsNull(subscribers.GetValue(Yes2ControlPlane.Instance));
            Assert.IsFalse(Yes2ControlPlane.Instance.Status.Available);
        }

        [Test]
        public void TryGetResource_ReturnsCallerSafeCopy()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            Assert.IsTrue(_controlPlane.TryGetResource("runtime.config", out var resource));
            Assert.AreEqual("runtime.config", resource.Key);
            Assert.AreEqual("config.v1", resource.TypeKey);
            Assert.AreEqual("rev-1", resource.RevisionId);
            var data = (JObject)resource.Data;
            data["speed"] = 99;

            Assert.AreEqual(2, (int)resource.Data["speed"]);
            Assert.IsTrue(_controlPlane.TryGetResource("runtime.config", out var again));
            Assert.AreEqual(2, (int)again.Data["speed"]);
        }

        [Test]
        public void TryGetResource_KeepsUnknownTypeInspectable()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            Assert.IsTrue(_controlPlane.TryGetResource("zz.future", out var resource));
            Assert.AreEqual("future.v9", resource.TypeKey);
            Assert.AreEqual(new JArray(1, 2).ToString(), resource.Data.ToString());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing")]
        [TestCase("RUNTIME.CONFIG")]
        public void TryGetResource_MissingKeyReturnsFalse(string key)
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            Assert.IsFalse(_controlPlane.TryGetResource(key, out var resource));
            Assert.IsNull(resource);
        }

        [Test]
        public void ClearCache_RemovesLkgAndKeepsActiveDeployment()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            _controlPlane.ClearCacheAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNull(Journal().Load());
            AssertActive(_controlPlane.Status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            Assert.IsTrue(_controlPlane.TryGetResource("runtime.config", out _));
        }

        [Test]
        public void ClearCache_HonoursCancellationAndNoopsBeforeInitialize()
        {
            Assert.IsTrue(_controlPlane.ClearCacheAsync(new CancellationToken(true)).IsCanceled);
            Assert.IsTrue(_controlPlane.ClearCacheAsync(CancellationToken.None).IsCompleted);
        }

        [Test]
        public void DeploymentActivated_ThrowingHandlerDoesNotBreakActivation()
        {
            _controlPlane.DeploymentActivated += _ => throw new InvalidOperationException("handler failed");
            Serve(Bootstrap1, Snapshot1);
            LogAssert.Expect(LogType.Exception, new Regex("handler failed"));

            AssertActive(Initialize(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);
            Assert.AreEqual("dep-1", Journal().Load().Bootstrap.DeploymentId);
        }

        [Test]
        public void Schedule_RefreshesEveryIntervalWithTenPercentJitter()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            for (var cycle = 1; cycle <= 20; cycle++)
            {
                _timer.Elapse();
                Assert.AreEqual(cycle + 1, BootstrapRequests);
            }

            Assert.AreEqual(21, _timer.Waits.Count);
            Assert.IsTrue(_timer.Waits.All(wait => wait >= TimeSpan.FromSeconds(54) && wait <= TimeSpan.FromSeconds(66)), string.Join(", ", _timer.Waits));
            Assert.Less(_timer.Waits.Min(), TimeSpan.FromSeconds(56), string.Join(", ", _timer.Waits));
            Assert.Greater(_timer.Waits.Max(), TimeSpan.FromSeconds(64), string.Join(", ", _timer.Waits));
            Assert.AreEqual(1, _transport.Requests.Count(url => url == ControlPlaneFixture.SnapshotUrl));
            AssertActivations("dep-1");
        }

        [Test]
        public void Schedule_IntervalZeroRunsNoLoop()
        {
            Serve(Bootstrap1, Snapshot1);
            var options = Options();
            options.RefreshIntervalSeconds = 0;

            AssertActive(_controlPlane.InitializeAsync(options, CancellationToken.None).GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", null);

            CollectionAssert.IsEmpty(_timer.Waits);
            Assert.AreEqual(1, BootstrapRequests);
        }

        [Test]
        public void Schedule_StartsAfterPreCancelledInitialize()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize(new CancellationToken(true));

            _timer.Elapse();

            AssertActive(_controlPlane.Status, ControlPlaneSource.Remote, "dep-1", "rel-1", null);
        }

        [Test]
        public void Schedule_PausedOverdueCycleRefreshesAtOnceOnResume()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            _controlPlane.SetApplicationPaused(true);

            _timer.Elapse();
            Assert.AreEqual(1, BootstrapRequests);

            _controlPlane.SetApplicationPaused(false);
            Assert.AreEqual(2, BootstrapRequests);
            Assert.IsTrue(_timer.Waiting);
            Assert.AreEqual(2, _timer.Waits.Count);
        }

        [Test]
        public void Schedule_RepeatedPauseKeepsTheCycleWaitingUntilResume()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            _controlPlane.SetApplicationPaused(true);
            _timer.Elapse();

            _controlPlane.SetApplicationPaused(true);
            Assert.AreEqual(1, BootstrapRequests);

            _controlPlane.SetApplicationPaused(false);
            Assert.AreEqual(2, BootstrapRequests);
        }

        [Test]
        public void Schedule_ResumeBeforeDueWaitsForTheInterval()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();

            _controlPlane.SetApplicationPaused(true);
            _controlPlane.SetApplicationPaused(false);
            Assert.AreEqual(1, BootstrapRequests);

            _timer.Elapse();
            Assert.AreEqual(2, BootstrapRequests);
        }

        [Test]
        public void Schedule_ManualCallerTokenNeverCancelsAutomaticCycle()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, Snapshot2));
            _timer.Elapse();

            using (var cancellation = new CancellationTokenSource())
            {
                var manual = _controlPlane.RefreshAsync(cancellation.Token);
                cancellation.Cancel();
                AssertActive(manual.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", "cancelled");
            }

            Assert.IsFalse(pending.Task.IsCompleted);
            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap2));
            AssertActive(_controlPlane.Status, ControlPlaneSource.Remote, "dep-2", "rel-2", null);
            Assert.AreEqual(2, BootstrapRequests);
            AssertActivations("dep-1", "dep-2");
            Assert.IsTrue(_timer.Waiting);
        }

        [Test]
        public void Schedule_AutomaticCycleJoinsManualRefreshAndOutlivesItsToken()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            _transport.Respond(ControlPlaneFixture.SnapshotUrl, ControlPlaneTransportResponse.Completed(200, Snapshot2));

            using (var cancellation = new CancellationTokenSource())
            {
                var manual = _controlPlane.RefreshAsync(cancellation.Token);
                _timer.Elapse();
                Assert.AreEqual(2, BootstrapRequests);
                cancellation.Cancel();
                AssertActive(manual.GetAwaiter().GetResult(), ControlPlaneSource.Remote, "dep-1", "rel-1", "cancelled");
            }

            Assert.IsFalse(pending.Task.IsCompleted);
            Assert.IsFalse(_timer.Waiting);
            pending.SetResult(ControlPlaneTransportResponse.Completed(200, Bootstrap2));
            AssertActive(_controlPlane.Status, ControlPlaneSource.Remote, "dep-2", "rel-2", null);
            Assert.IsTrue(_timer.Waiting);
            Assert.AreEqual(2, _timer.Waits.Count);
        }

        [Test]
        public void Shutdown_DuringAutomaticCycleCancelsItAndStopsTheLoop()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            var pending = _transport.Hold(ControlPlaneFixture.BootstrapUrl);
            _timer.Elapse();

            _controlPlane.Shutdown();

            Assert.IsTrue(pending.Task.IsCanceled);
            Assert.IsFalse(_timer.Waiting);
            Assert.AreEqual(1, _timer.Waits.Count);
        }

        [Test]
        public void Shutdown_WhilePausedStopsTheLoop()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            _controlPlane.SetApplicationPaused(true);
            _timer.Elapse();

            _controlPlane.Shutdown();
            _controlPlane.SetApplicationPaused(false);

            Assert.AreEqual(1, BootstrapRequests);
            Assert.AreEqual(1, _timer.Waits.Count);
        }

        [Test]
        public void Reset_StopsTheLoopAndClearsPause()
        {
            Serve(Bootstrap1, Snapshot1);
            Initialize();
            _controlPlane.SetApplicationPaused(true);

            _controlPlane.Reset();
            Assert.IsFalse(_timer.Waiting);

            Initialize();
            _timer.Elapse();
            Assert.AreEqual(3, BootstrapRequests);
        }

        [Test]
        public void Entry_ObservesApplicationEventsBeforeTheFirstScene()
        {
            var observe = typeof(Yes2ControlPlane).GetMethod("ObserveApplication", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.AreEqual(RuntimeInitializeLoadType.BeforeSceneLoad, observe.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>().loadType);
        }

        [Test]
        public void ApplicationEvents_ForwardPauseAndQuitToTheInstance()
        {
            var paused = typeof(Yes2SDKControlPlane).GetField("_paused", BindingFlags.Instance | BindingFlags.NonPublic);
            var go = new GameObject("ControlPlaneApplicationEventsTest");
            try
            {
                var events = go.AddComponent<ControlPlaneApplicationEvents>();
                Message(events, "OnApplicationPause", true);
                Assert.IsTrue((bool)paused.GetValue(Yes2ControlPlane.Instance));
                Message(events, "OnApplicationPause", false);
                Assert.IsFalse((bool)paused.GetValue(Yes2ControlPlane.Instance));

                var options = Options();
                options.RuntimeOrigin = "https://127.0.0.1";
                options.RefreshIntervalSeconds = 0;
                Yes2ControlPlane.Instance.InitializeAsync(options, new CancellationToken(true)).GetAwaiter().GetResult();
                Assert.AreEqual(ControlPlaneFixture.Game, Yes2ControlPlane.Instance.Status.GameKey);

                Message(events, "OnApplicationQuit");

                Assert.AreEqual(ControlPlaneSource.None, Yes2ControlPlane.Instance.Status.Source);
                Assert.IsNull(Yes2ControlPlane.Instance.Status.GameKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                Yes2ControlPlane.Instance.Reset();
            }
        }

        private static void Message(MonoBehaviour behaviour, string name, params object[] arguments)
        {
            var parameters = arguments.Select(argument => argument.GetType()).ToArray();
            behaviour.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, parameters, null).Invoke(behaviour, arguments);
        }

        [Test]
        public void Assembly_HasNoAsyncVoidMethods()
        {
            var asyncVoid = typeof(Yes2SDKControlPlane).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(m => m.ReturnType == typeof(void) && m.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
                .Select(m => m.DeclaringType + "." + m.Name)
                .ToArray();

            CollectionAssert.IsEmpty(asyncVoid);
        }

        [Test]
        public void Entry_Yes2SDKControlPlaneIsTheSameInstanceAsYes2ControlPlane()
        {
            Assert.IsNotNull(Yes2ControlPlane.Instance);
            Assert.AreSame(Yes2ControlPlane.Instance, global::Yes2SDK.Yes2SDK.ControlPlane);
        }

        private static ControlPlaneTransportResponse Failure(string kind)
        {
            switch (kind)
            {
                case "network":
                    return ControlPlaneTransportResponse.NetworkUnavailable("offline");
                case "timeout":
                    return ControlPlaneTransportResponse.TimedOut();
                default:
                    return ControlPlaneTransportResponse.Completed(long.Parse(kind), null);
            }
        }

        private sealed class ManualTimer
        {
            private TaskCompletionSource<bool> _pending;

            public List<TimeSpan> Waits { get; } = new List<TimeSpan>();

            public bool Waiting => _pending != null && !_pending.Task.IsCompleted;

            public Task Delay(TimeSpan delay, CancellationToken token)
            {
                if (delay < TimeSpan.FromSeconds(1))
                {
                    return Task.CompletedTask;
                }
                if (token.IsCancellationRequested)
                {
                    return Task.FromCanceled(token);
                }
                Waits.Add(delay);
                var pending = new TaskCompletionSource<bool>();
                token.Register(() => pending.TrySetCanceled());
                _pending = pending;
                return pending.Task;
            }

            public void Elapse()
            {
                Assert.IsTrue(Waiting);
                _pending.SetResult(true);
            }
        }

        private sealed class RoutingTransport : IControlPlaneTransport
        {
            private readonly Dictionary<string, Func<CancellationToken, Task<ControlPlaneTransportResponse>>> _routes = new Dictionary<string, Func<CancellationToken, Task<ControlPlaneTransportResponse>>>();

            public List<string> Requests { get; } = new List<string>();

            public void Respond(string url, ControlPlaneTransportResponse response)
            {
                _routes[url] = _ => Task.FromResult(response);
            }

            public TaskCompletionSource<ControlPlaneTransportResponse> Hold(string url, bool honourCancellation = true)
            {
                var pending = new TaskCompletionSource<ControlPlaneTransportResponse>();
                _routes[url] = token =>
                {
                    if (honourCancellation)
                    {
                        token.Register(() => pending.TrySetCanceled());
                    }
                    return pending.Task;
                };
                return pending;
            }

            public Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
            {
                Requests.Add(request.Url);
                return _routes.TryGetValue(request.Url, out var route)
                    ? route(token)
                    : Task.FromResult(ControlPlaneTransportResponse.Completed(404, null));
            }
        }
    }
}
