using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    public class ControlPlaneLiveOpsTests
    {
        private const string SnapshotUrl = "https://runtime.yes2games.com/releases/{0}/runtime-snapshot-v1.json";
        private const string FixtureRelease = "rel_01j9z3k5w8e2r4t6y8u0i2o4p6";
        private const string Winter = "sha256:b0940a72356dfccd36a77a007ad31ef4709155d5bfa4aef325f229b1700dca2e";

        private string _root;
        private Transport _transport;
        private Yes2SDKControlPlane _controlPlane;
        private SynchronizationContext _context;
        private DateTimeOffset _now;

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _root = Path.Combine(Path.GetTempPath(), "yes2sdk-cp-liveops-" + Guid.NewGuid().ToString("N"));
            _transport = new Transport();
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

        private ControlPlaneLiveOps LiveOps => _controlPlane.LiveOps;

        private Yes2SDKControlPlane NewControlPlane()
        {
            var controlPlane = new Yes2SDKControlPlane(
                _transport,
                (delay, token) => Task.CompletedTask,
                new System.Random(0),
                (game, environment) => new ControlPlaneLkgJournal(_root, game, environment));
            controlPlane.LiveOps.Clock = () => _now;
            return controlPlane;
        }

        private static string FixtureSnapshot(string name)
        {
            return Path.Combine(ControlPlaneContractFixtureTests.Fixtures, "valid", "runtime-snapshot-v1", name + ".json");
        }

        private static byte[] SnapshotBytes(string releaseId, IEnumerable<string> artifacts, params string[] resources)
        {
            var descriptors = artifacts.OrderBy(digest => digest, StringComparer.Ordinal)
                .Select(digest => "{\"digest\":\"" + digest + "\",\"mediaType\":\"application/json\",\"size\":1,\"url\":\"https://runtime.yes2games.com/a/" + digest.Substring(7) + "\"}");
            return Encoding.UTF8.GetBytes(
                "{\"protocolVersion\":1,\"gameKey\":\"nsr\",\"releaseId\":\"" + releaseId + "\",\"resources\":[" + string.Join(",", resources) + "],\"artifacts\":[" + string.Join(",", descriptors) + "]}");
        }

        private static string Resource(string key, string typeKey, string revisionId, string data)
        {
            return "{\"key\":\"" + key + "\",\"typeKey\":\"" + typeKey + "\",\"revisionId\":\"" + revisionId + "\",\"data\":" + data + "}";
        }

        private static string Event(string key, string startAt, string endAt, string payload = "{}")
        {
            return Resource(key, "liveops.event.v1", "rev-" + key, "{\"startAt\":\"" + startAt + "\",\"endAt\":\"" + endAt + "\",\"payload\":" + payload + ",\"artifactRefs\":[]}");
        }

        private void Serve(string deploymentId, string releaseId, byte[] snapshot)
        {
            var url = string.Format(SnapshotUrl, releaseId);
            var bootstrap = new JObject
            {
                ["protocolVersion"] = 1,
                ["gameKey"] = ControlPlaneFixture.Game,
                ["environmentKey"] = ControlPlaneFixture.Environment,
                ["deploymentId"] = deploymentId,
                ["releaseId"] = releaseId,
                ["snapshotUrl"] = url,
                ["snapshotDigest"] = ControlPlaneSha256.Digest(snapshot)
            };
            _transport.Routes.Clear();
            _transport.Routes[ControlPlaneFixture.BootstrapUrl] = ControlPlaneFixture.Bytes(bootstrap);
            _transport.Routes[url] = snapshot;
        }

        private ControlPlaneStatus Initialize()
        {
            var options = new ControlPlaneOptions { GameKey = ControlPlaneFixture.Game, EnvironmentKey = ControlPlaneFixture.Environment, RefreshIntervalSeconds = 0 };
            return _controlPlane.InitializeAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        }

        private void InitializeFixture()
        {
            Serve("dep-1", FixtureRelease, File.ReadAllBytes(FixtureSnapshot("liveops-events")));
            Assert.AreEqual(ControlPlaneSource.Remote, Initialize().Source);
        }

        private static DateTimeOffset At(string instant)
        {
            return DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private string[] ActiveAt(string instant)
        {
            _now = At(instant);
            return LiveOps.GetActiveEvents().Select(e => e.ResourceKey).ToArray();
        }

        private static string Describe(LiveOpsEvent e)
        {
            return string.Join(" ", e.ResourceKey, e.RevisionId, e.StartAt.UtcTicks, e.EndAt.UtcTicks, e.PayloadJson, string.Join(",", e.ArtifactRefs), e.DeploymentId, e.ReleaseId);
        }

        [Test]
        public void LiveOps_IsTheSameInstanceThroughYes2SDK()
        {
            Assert.AreSame(Yes2ControlPlane.Instance.LiveOps, Yes2SDK.ControlPlane.LiveOps);
        }

        [Test]
        public void ProductionClock_IsUtcNow()
        {
            var before = DateTimeOffset.UtcNow;
            var now = Yes2ControlPlane.Instance.LiveOps.Clock();

            Assert.AreEqual(TimeSpan.Zero, now.Offset);
            Assert.That(now, Is.InRange(before, DateTimeOffset.UtcNow));
        }

        [Test]
        public void FixtureEvents_ExposeExactFields()
        {
            InitializeFixture();

            var events = LiveOps.GetEvents();

            CollectionAssert.AreEqual(new[] { "event.weekend", "event.winter" }, events.Select(e => e.ResourceKey));
            var winter = events[1];
            Assert.AreEqual("rev_winter_1", winter.RevisionId);
            Assert.AreEqual(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), winter.StartAt);
            Assert.AreEqual(new DateTimeOffset(2026, 12, 8, 0, 0, 0, TimeSpan.Zero), winter.EndAt);
            Assert.AreEqual(TimeSpan.Zero, winter.StartAt.Offset);
            Assert.AreEqual("{\"theme\":\"winter\"}", winter.PayloadJson);
            CollectionAssert.AreEqual(new[] { Winter }, winter.ArtifactRefs);
            Assert.AreEqual("dep-1", winter.DeploymentId);
            Assert.AreEqual(FixtureRelease, winter.ReleaseId);
            CollectionAssert.IsEmpty(events[0].ArtifactRefs);
        }

        [TestCase("2026-11-30T23:59:59.999Z")]
        [TestCase("2026-12-01T00:00:00Z", "event.winter")]
        [TestCase("2026-12-04T23:59:59.999Z", "event.winter")]
        [TestCase("2026-12-05T00:00:00Z", "event.weekend", "event.winter")]
        [TestCase("2026-12-06T12:00:00Z", "event.weekend", "event.winter")]
        [TestCase("2026-12-06T23:59:59.999Z", "event.weekend", "event.winter")]
        [TestCase("2026-12-07T00:00:00Z", "event.winter")]
        [TestCase("2026-12-07T23:59:59.999Z", "event.winter")]
        [TestCase("2026-12-08T00:00:00Z")]
        [TestCase("2027-01-01T00:00:00Z")]
        public void ActiveEvents_AreHalfOpenAndMayOverlap(string instant, params string[] expected)
        {
            InitializeFixture();

            CollectionAssert.AreEqual(expected, ActiveAt(instant));
            Assert.AreEqual(2, LiveOps.GetEvents().Count);
        }

        [Test]
        public void ActiveEvents_HonourOneTickAroundEveryBoundary()
        {
            InitializeFixture();
            var winter = LiveOps.GetEvents()[1];

            _now = winter.StartAt.AddTicks(-1);
            CollectionAssert.DoesNotContain(LiveOps.GetActiveEvents(), winter);
            _now = winter.StartAt;
            CollectionAssert.Contains(LiveOps.GetActiveEvents(), winter);
            _now = winter.EndAt.AddTicks(-1);
            CollectionAssert.Contains(LiveOps.GetActiveEvents(), winter);
            _now = winter.EndAt;
            CollectionAssert.DoesNotContain(LiveOps.GetActiveEvents(), winter);
        }

        [Test]
        public void ActiveEvents_CompareInstantsNotOffsets()
        {
            InitializeFixture();

            _now = new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.FromHours(7));
            CollectionAssert.AreEqual(new[] { "event.winter" }, LiveOps.GetActiveEvents().Select(e => e.ResourceKey));
            _now = new DateTimeOffset(2026, 12, 1, 6, 59, 59, TimeSpan.FromHours(7));
            CollectionAssert.IsEmpty(LiveOps.GetActiveEvents());
        }

        [Test]
        public void EveryValidLiveOpsFixture_IsQueryableAtItsBoundaries()
        {
            var fixtures = Directory.GetFiles(Path.Combine(ControlPlaneContractFixtureTests.Fixtures, "valid", "liveops-event-v1"), "*.json")
                .Where(path => !path.EndsWith(".expected.json"))
                .ToDictionary(path => "fixture." + Path.GetFileNameWithoutExtension(path), path => (JObject)ControlPlaneStrictJson.Parse(File.ReadAllBytes(path), 0), StringComparer.Ordinal);
            var keys = fixtures.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            var artifacts = fixtures.Values.SelectMany(data => data["artifactRefs"].Select(digest => (string)digest)).Distinct().ToArray();
            var snapshot = SnapshotBytes("rel-1", artifacts,
                keys.Select(key => Resource(key, "liveops.event.v1", "rev-1", File.ReadAllText(Path.Combine(ControlPlaneContractFixtureTests.Fixtures, "valid", "liveops-event-v1", key.Substring("fixture.".Length) + ".json")))).ToArray());
            var read = ControlPlaneDocuments.ReadSnapshot(snapshot, new ControlPlaneBootstrap("nsr", "production", "dep-1", "rel-1", string.Format(SnapshotUrl, "rel-1"), ControlPlaneSha256.Digest(snapshot)));
            Assert.IsTrue(read.Succeeded, read.Detail);
            Serve("dep-1", "rel-1", snapshot);
            Assert.AreEqual(ControlPlaneSource.Remote, Initialize().Source);

            var events = LiveOps.GetEvents();
            Assert.AreEqual(11, events.Count);
            CollectionAssert.AreEqual(keys, events.Select(e => e.ResourceKey));
            var mismatches = new List<string>();
            foreach (var e in events)
            {
                var data = fixtures[e.ResourceKey];
                if (!JToken.DeepEquals(data["payload"], ControlPlaneStrictJson.Parse(e.PayloadJson)))
                {
                    mismatches.Add(e.ResourceKey + " payload");
                }
                if (!data["artifactRefs"].Select(digest => (string)digest).SequenceEqual(e.ArtifactRefs))
                {
                    mismatches.Add(e.ResourceKey + " artifactRefs");
                }
                if (e.StartAt != At((string)data["startAt"]) || e.EndAt != At((string)data["endAt"]))
                {
                    mismatches.Add(e.ResourceKey + " instants " + e.StartAt.ToString("o") + " " + e.EndAt.ToString("o"));
                }
                var boundaries = new[]
                {
                    Active(e, e.StartAt.UtcTicks == 0 ? (DateTimeOffset?)null : e.StartAt.AddTicks(-1)),
                    Active(e, e.StartAt),
                    Active(e, e.EndAt.AddTicks(-1)),
                    Active(e, e.EndAt)
                };
                if (!boundaries.SequenceEqual(new bool?[] { e.StartAt.UtcTicks == 0 ? (bool?)null : false, true, true, false }))
                {
                    mismatches.Add(e.ResourceKey + " boundaries " + string.Join(",", boundaries));
                }
            }
            CollectionAssert.IsEmpty(mismatches);
            Assert.AreEqual(DateTimeOffset.MinValue, events.Single(e => e.ResourceKey == "fixture.widest-range").StartAt);
            Assert.AreEqual(new DateTimeOffset(9999, 12, 31, 23, 59, 59, 999, TimeSpan.Zero), events.Single(e => e.ResourceKey == "fixture.widest-range").EndAt);
            Assert.AreEqual(500, events.Single(e => e.ResourceKey == "fixture.fraction-1-digit").StartAt.Millisecond);
            Assert.AreEqual(TimeSpan.FromMilliseconds(1), events.Single(e => e.ResourceKey == "fixture.end-fraction-only").EndAt - events.Single(e => e.ResourceKey == "fixture.end-fraction-only").StartAt);
        }

        private bool? Active(LiveOpsEvent e, DateTimeOffset? instant)
        {
            if (instant == null)
            {
                return null;
            }
            _now = instant.Value;
            return LiveOps.GetActiveEvents().Contains(e);
        }

        [Test]
        public void GetEvents_IncludesUpcomingActiveAndEndedDefinitions()
        {
            Serve("dep-1", "rel-1", SnapshotBytes("rel-1", new string[0],
                Event("a.ended", "2026-01-01T00:00:00Z", "2026-02-01T00:00:00Z"),
                Event("b.active", "2026-11-01T00:00:00Z", "2026-12-01T00:00:00Z"),
                Event("c.upcoming", "2027-01-01T00:00:00Z", "2027-02-01T00:00:00Z"),
                Resource("runtime.config", "config.v1", "rev-1", "{\"startAt\":\"2026-01-01T00:00:00Z\"}"),
                Resource("zz.future", "future.v9", "rev-9", "{\"startAt\":\"2026-01-01T00:00:00Z\"}")));
            Initialize();
            _now = At("2026-11-11T00:00:00Z");

            CollectionAssert.AreEqual(new[] { "a.ended", "b.active", "c.upcoming" }, LiveOps.GetEvents().Select(e => e.ResourceKey));
            CollectionAssert.AreEqual(new[] { "b.active" }, LiveOps.GetActiveEvents().Select(e => e.ResourceKey));
        }

        [Test]
        public void Events_AreInOrdinalAsciiOrderWhateverTheCulture()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                var keys = new[] { "a-b", "a.c", "a0", "a_a", "ab", "i.event", "z" };
                Serve("dep-1", "rel-1", SnapshotBytes("rel-1", new string[0], keys.Select(key => Event(key, "2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z")).ToArray()));
                Initialize();
                _now = At("2026-06-01T00:00:00Z");

                CollectionAssert.AreEqual(keys, LiveOps.GetEvents().Select(e => e.ResourceKey));
                CollectionAssert.AreEqual(keys, LiveOps.GetActiveEvents().Select(e => e.ResourceKey));
                CollectionAssert.AreNotEqual(keys, keys.OrderBy(key => key, StringComparer.CurrentCulture));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [Test]
        public void Read_SortsOrdinallyWhateverTheSnapshotOrder()
        {
            var root = new JObject
            {
                ["resources"] = new JArray(new[] { "z", "ab", "a_a", "a0", "a.c", "a-b" }.Select(key => ControlPlaneStrictJson.Parse(Encoding.UTF8.GetBytes(Event(key, "2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z")), 0)))
            };
            var snapshot = new ControlPlaneSnapshot(ControlPlaneFixture.Deployment("dep-1", "rel-1").Bootstrap, null, root);

            CollectionAssert.AreEqual(new[] { "a-b", "a.c", "a0", "a_a", "ab", "z" }, ControlPlaneLiveOps.Read(snapshot).Select(e => e.ResourceKey));
        }

        [Test]
        public void NoEvents_IsAnImmutableEmptyList()
        {
            var lists = new List<IReadOnlyList<LiveOpsEvent>> { LiveOps.GetEvents(), LiveOps.GetActiveEvents() };
            Serve("dep-1", "rel-1", SnapshotBytes("rel-1", new string[0], Resource("runtime.config", "config.v1", "rev-1", "{}")));
            Initialize();
            lists.Add(LiveOps.GetEvents());
            lists.Add(LiveOps.GetActiveEvents());

            foreach (var list in lists)
            {
                CollectionAssert.IsEmpty(list);
                Assert.IsTrue(((ICollection<LiveOpsEvent>)list).IsReadOnly);
                Assert.Throws<NotSupportedException>(() => ((ICollection<LiveOpsEvent>)list).Add(null));
            }
            Assert.IsFalse(LiveOps.TryGetEvent("runtime.config", out var none));
            Assert.IsNull(none);
        }

        [Test]
        public void TryGetEvent_MatchesExactKeyOfALiveOpsResource()
        {
            InitializeFixture();

            Assert.IsTrue(LiveOps.TryGetEvent("event.winter", out var winter));
            Assert.AreEqual("rev_winter_1", winter.RevisionId);
            Assert.IsFalse(LiveOps.TryGetEvent("EVENT.WINTER", out var upper));
            Assert.IsNull(upper);
            Assert.IsFalse(LiveOps.TryGetEvent("event.winter ", out _));
            Assert.IsFalse(LiveOps.TryGetEvent("event", out _));
            Assert.IsFalse(LiveOps.TryGetEvent("runtime.config", out _));
            Assert.IsFalse(LiveOps.TryGetEvent("event.missing", out _));
            Assert.IsFalse(LiveOps.TryGetEvent(null, out _));
        }

        [Test]
        public void ReturnedValues_CannotMutateActiveState()
        {
            InitializeFixture();
            _now = At("2026-12-05T00:00:00Z");
            var before = LiveOps.GetEvents().Select(Describe).ToArray();

            Assert.Throws<NotSupportedException>(() => ((IList<LiveOpsEvent>)LiveOps.GetEvents())[0] = null);
            Assert.Throws<NotSupportedException>(() => ((IList<LiveOpsEvent>)LiveOps.GetActiveEvents()).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)LiveOps.GetEvents()[1].ArtifactRefs)[0] = "sha256:" + new string('0', 64));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)LiveOps.GetEvents()[1].ArtifactRefs).Add("x"));
            var payload = JObject.Parse(LiveOps.GetEvents()[1].PayloadJson);
            payload["theme"] = "summer";
            Assert.IsTrue(_controlPlane.TryGetResource("event.winter", out var resource));
            resource.Data["payload"]["theme"] = "summer";
            ((JArray)resource.Data["artifactRefs"]).Clear();

            CollectionAssert.AreEqual(before, LiveOps.GetEvents().Select(Describe));
            CollectionAssert.AreEqual(before, LiveOps.GetActiveEvents().Select(Describe));
            foreach (var property in typeof(LiveOpsEvent).GetProperties())
            {
                Assert.IsNull(property.GetSetMethod(true), property.Name);
            }
        }

        [Test]
        public void LkgAndRemote_AnswerTheSameQueries()
        {
            InitializeFixture();
            var remote = LiveOps.GetEvents().Select(Describe).ToArray();
            _controlPlane.Shutdown();
            _transport.Routes.Clear();
            _controlPlane = NewControlPlane();

            var status = Initialize();

            Assert.AreEqual(ControlPlaneSource.Lkg, status.Source);
            CollectionAssert.AreEqual(remote, LiveOps.GetEvents().Select(Describe));
            CollectionAssert.AreEqual(new[] { "event.weekend", "event.winter" }, ActiveAt("2026-12-05T00:00:00Z"));
        }

        [Test]
        public void DeploymentActivated_SwapsTheEventSetAtOnce()
        {
            Serve("dep-1", "rel-1", SnapshotBytes("rel-1", new string[0],
                Event("a.event", "2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z", "{\"v\":1}"),
                Event("b.event", "2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z", "{\"v\":1}")));
            Initialize();
            _now = At("2026-06-01T00:00:00Z");
            var old = LiveOps.GetEvents();
            var seen = new List<string>();
            _controlPlane.DeploymentActivated += activation => seen.Add(activation.DeploymentId + " " + string.Join(";", LiveOps.GetActiveEvents().Select(e => e.ResourceKey + "=" + e.PayloadJson + "@" + e.DeploymentId)));

            Serve("dep-2", "rel-2", SnapshotBytes("rel-2", new string[0],
                Event("b.event", "2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z", "{\"v\":2}"),
                Event("c.event", "2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z", "{\"v\":2}")));
            _controlPlane.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            _controlPlane.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "dep-2 b.event={\"v\":2}@dep-2;c.event={\"v\":2}@dep-2" }, seen);
            CollectionAssert.AreEqual(new[] { "a.event", "b.event" }, old.Select(e => e.ResourceKey));
            Assert.AreEqual("dep-1", old[1].DeploymentId);
        }

        [Test]
        public void CrossingAScheduleBoundary_NeedsNoTransportCall()
        {
            InitializeFixture();
            var calls = _transport.Calls;

            CollectionAssert.IsEmpty(ActiveAt("2026-11-30T00:00:00Z"));
            CollectionAssert.AreEqual(new[] { "event.winter" }, ActiveAt("2026-12-01T00:00:00Z"));
            CollectionAssert.AreEqual(new[] { "event.weekend", "event.winter" }, ActiveAt("2026-12-05T00:00:00Z"));
            CollectionAssert.IsEmpty(ActiveAt("2026-12-08T00:00:00Z"));
            Assert.IsTrue(LiveOps.TryGetEvent("event.weekend", out _));

            Assert.AreEqual(calls, _transport.Calls);
        }

        [Test]
        public void LiveOps_HasOnlyTheThreeQueriesAndNoEventTimerOrGameLogic()
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            CollectionAssert.AreEquivalent(new[] { "GetEvents", "GetActiveEvents", "TryGetEvent" }, typeof(ControlPlaneLiveOps).GetMethods(flags).Select(m => m.Name));
            CollectionAssert.IsEmpty(typeof(ControlPlaneLiveOps).GetProperties(flags));
            CollectionAssert.IsEmpty(typeof(ControlPlaneLiveOps).GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            CollectionAssert.IsEmpty(typeof(ControlPlaneLiveOps).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(f => f.FieldType != typeof(Func<ControlPlaneActiveState>) && f.FieldType != typeof(Func<DateTimeOffset>)));
            CollectionAssert.AreEquivalent(
                new[] { "ResourceKey", "RevisionId", "StartAt", "EndAt", "PayloadJson", "ArtifactRefs", "DeploymentId", "ReleaseId" },
                typeof(LiveOpsEvent).GetProperties(flags).Select(p => p.Name));
            CollectionAssert.IsEmpty(typeof(LiveOpsEvent).GetMethods(flags).Where(m => !m.IsSpecialName));
        }

        private sealed class Transport : IControlPlaneTransport
        {
            public Dictionary<string, byte[]> Routes { get; } = new Dictionary<string, byte[]>();
            public int Calls { get; private set; }

            public Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
            {
                Calls++;
                return Task.FromResult(Routes.TryGetValue(request.Url, out var body)
                    ? ControlPlaneTransportResponse.Completed(200, body)
                    : ControlPlaneTransportResponse.Completed(404, null));
            }
        }
    }
}
