using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Yes2SDK.Tests
{
    public class ControlPlaneConfigTests
    {
        private const string SnapshotUrl = "https://runtime.yes2games.com/releases/{0}/runtime-snapshot-v1.json";

        private const string RuntimeConfig = "{\"flag\":true,\"off\":false,\"count\":42,\"integral\":3.0,\"exponent\":1e3,\"fraction\":1.5," +
            "\"max\":9007199254740991,\"min\":-9007199254740991,\"over\":9007199254740992,\"under\":-9007199254740992,\"huge\":100000000000000000000," +
            "\"text\":\"h\\u00e9llo \\ud83d\\ude00\",\"numeric\":\"1\",\"truthy\":\"true\",\"one\":1,\"zero\":0,\"empty\":\"\"," +
            "\"list\":[10,{\"x\":\"y\"}],\"nested\":{\"deep\":{\"leaf\":\"v\"}},\"a/b\":\"slash\",\"m~n\":\"tilde\"}";

        private string _root;
        private Transport _transport;
        private Yes2SDKControlPlane _controlPlane;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _root = Path.Combine(Path.GetTempPath(), "yes2sdk-cp-config-" + Guid.NewGuid().ToString("N"));
            _transport = new Transport();
            _controlPlane = new Yes2SDKControlPlane(
                _transport,
                (delay, token) => Task.CompletedTask,
                new System.Random(0),
                (game, environment) => Journal());
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

        private ControlPlaneConfig Config => _controlPlane.Config;

        private ControlPlaneLkgJournal Journal()
        {
            return new ControlPlaneLkgJournal(_root, ControlPlaneFixture.Game, ControlPlaneFixture.Environment);
        }

        private static byte[] SnapshotBytes(string releaseId, params string[] resources)
        {
            return Encoding.UTF8.GetBytes(
                "{\"protocolVersion\":1,\"gameKey\":\"nsr\",\"releaseId\":\"" + releaseId + "\",\"resources\":[" + string.Join(",", resources) + "],\"artifacts\":[]}");
        }

        private static string Resource(string key, string typeKey, string revisionId, string data)
        {
            return "{\"key\":\"" + key + "\",\"typeKey\":\"" + typeKey + "\",\"revisionId\":\"" + revisionId + "\",\"data\":" + data + "}";
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
            _transport.Routes[ControlPlaneFixture.BootstrapUrl] = ControlPlaneFixture.Bytes(bootstrap);
            _transport.Routes[url] = snapshot;
        }

        private ControlPlaneStatus Initialize()
        {
            var options = new ControlPlaneOptions { GameKey = ControlPlaneFixture.Game, EnvironmentKey = ControlPlaneFixture.Environment, RefreshIntervalSeconds = 0 };
            return _controlPlane.InitializeAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        }

        private ControlPlaneStatus Refresh()
        {
            return _controlPlane.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private void InitializeRuntimeConfig()
        {
            Serve("dep-1", "rel-1", SnapshotBytes("rel-1",
                Resource("runtime.config", "config.v1", "rev-1", RuntimeConfig),
                Resource("shop.config", "config.v1", "rev-7", "{\"flag\":false,\"price\":250}"),
                Resource("summer.event", "liveops.event.v1", "rev-3",
                    "{\"startAt\":\"2026-06-01T00:00:00Z\",\"endAt\":\"2026-07-01T00:00:00Z\",\"payload\":{\"flag\":true},\"artifactRefs\":[]}"),
                Resource("zz.future", "future.v9", "rev-9", "{\"flag\":true}")));
            Assert.AreEqual(ControlPlaneSource.Remote, Initialize().Source);
        }

        [Test]
        public void ControlPlaneConfig_IsTheSameInstanceThroughYes2SDK()
        {
            Assert.AreSame(Yes2ControlPlane.Instance.Config, Yes2SDK.ControlPlane.Config);
        }

        [Test]
        public void TypedReads_ReturnExactRemoteValues()
        {
            InitializeRuntimeConfig();

            Assert.IsTrue(Config.GetBool("runtime.config", "/flag", false));
            Assert.IsFalse(Config.GetBool("runtime.config", "/off", true));
            Assert.AreEqual(42L, Config.GetInteger("runtime.config", "/count", -1));
            Assert.AreEqual(1.5, Config.GetNumber("runtime.config", "/fraction", -1));
            Assert.AreEqual(42.0, Config.GetNumber("runtime.config", "/count", -1));
            Assert.AreEqual(1e20, Config.GetNumber("runtime.config", "/huge", -1));
            Assert.AreEqual(9007199254740992.0, Config.GetNumber("runtime.config", "/over", -1));
            Assert.AreEqual("héllo 😀", Config.GetString("runtime.config", "/text", "default"));
            Assert.AreEqual("", Config.GetString("runtime.config", "/empty", "default"));
            Assert.AreEqual("y", Config.GetString("runtime.config", "/list/1/x", "default"));
            Assert.AreEqual(10L, Config.GetInteger("runtime.config", "/list/0", -1));
            Assert.AreEqual("v", Config.GetString("runtime.config", "/nested/deep/leaf", "default"));
            Assert.AreEqual("slash", Config.GetString("runtime.config", "/a~1b", "default"));
            Assert.AreEqual("tilde", Config.GetString("runtime.config", "/m~0n", "default"));
        }

        [TestCase("/max", 9007199254740991L)]
        [TestCase("/min", -9007199254740991L)]
        [TestCase("/integral", 3L)]
        [TestCase("/exponent", 1000L)]
        [TestCase("/over", -7L)]
        [TestCase("/under", -7L)]
        [TestCase("/huge", -7L)]
        [TestCase("/fraction", -7L)]
        public void GetInteger_AcceptsOnlySafeIntegralNumbers(string pointer, long expected)
        {
            InitializeRuntimeConfig();

            Assert.AreEqual(expected, Config.GetInteger("runtime.config", pointer, -7));
        }

        [Test]
        public void TypedReads_NeverCoerce()
        {
            InitializeRuntimeConfig();

            Assert.IsFalse(Config.GetBool("runtime.config", "/truthy", false));
            Assert.IsFalse(Config.GetBool("runtime.config", "/one", false));
            Assert.IsTrue(Config.GetBool("runtime.config", "/zero", true));
            Assert.AreEqual(-1L, Config.GetInteger("runtime.config", "/numeric", -1));
            Assert.AreEqual(-1L, Config.GetInteger("runtime.config", "/flag", -1));
            Assert.AreEqual(-1.0, Config.GetNumber("runtime.config", "/numeric", -1));
            Assert.AreEqual(-1.0, Config.GetNumber("runtime.config", "/flag", -1));
            Assert.AreEqual("default", Config.GetString("runtime.config", "/count", "default"));
            Assert.AreEqual("default", Config.GetString("runtime.config", "/flag", "default"));
            Assert.AreEqual("default", Config.GetString("runtime.config", "/nested", "default"));
        }

        [Test]
        public void TypeMismatch_LogsOneWarningPerDistinctRead()
        {
            InitializeRuntimeConfig();

            var warnings = Warnings(() =>
            {
                Config.GetBool("runtime.config", "/truthy", false);
                Config.GetBool("runtime.config", "/truthy", false);
                Config.GetString("runtime.config", "/count", "default");
            });

            CollectionAssert.AreEqual(new[]
            {
                "[Yes2SDK] ControlPlane.Config TypeMismatch for runtime.config /truthy: is string, not boolean; returning the default",
                "[Yes2SDK] ControlPlane.Config TypeMismatch for runtime.config /count: is integer, not string; returning the default"
            }, warnings);
        }

        [Test]
        public void Diagnostics_AreBounded()
        {
            InitializeRuntimeConfig();

            var warnings = Warnings(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    Config.GetBool("zz.future", "/flag/" + i, false);
                }
            });

            Assert.AreEqual(64, warnings.Count);
        }

        private static List<string> Warnings(Action action)
        {
            var warnings = new List<string>();
            void Log(string message, string stack, LogType type)
            {
                if (type == LogType.Warning)
                {
                    warnings.Add(message);
                }
            }
            Application.logMessageReceived += Log;
            try
            {
                action();
            }
            finally
            {
                Application.logMessageReceived -= Log;
            }
            return warnings;
        }

        [Test]
        public void MultipleConfigResources_ReadIndependently()
        {
            InitializeRuntimeConfig();

            Assert.IsTrue(Config.GetBool("runtime.config", "/flag", false));
            Assert.IsFalse(Config.GetBool("shop.config", "/flag", true));
            Assert.AreEqual(250L, Config.GetInteger("shop.config", "/price", -1));
            Assert.AreEqual(-1L, Config.GetInteger("runtime.config", "/price", -1));
            Assert.AreEqual("rev-7", Config.Inspect("shop.config").RevisionId);
        }

        [Test]
        public void Inspect_DistinguishesEveryState()
        {
            Assert.AreEqual(ConfigLookupState.NoActiveControlPlane, Config.Inspect("runtime.config", "/flag").State);
            InitializeRuntimeConfig();

            var value = Config.Inspect("runtime.config", "/flag");
            Assert.AreEqual(ConfigLookupState.Value, value.State);
            Assert.AreEqual(ControlPlaneSource.Remote, value.Source);
            Assert.AreEqual("runtime.config", value.ResourceKey);
            Assert.AreEqual("/flag", value.Pointer);
            Assert.AreEqual("rev-1", value.RevisionId);
            Assert.AreEqual("dep-1", value.DeploymentId);
            Assert.AreEqual("rel-1", value.ReleaseId);
            Assert.IsTrue(JToken.DeepEquals(new JValue(true), value.Value));

            Assert.AreEqual(ConfigLookupState.ResourceMissing, Config.Inspect("missing.config", "/flag").State);
            Assert.AreEqual(ConfigLookupState.PathMissing, Config.Inspect("runtime.config", "/missing").State);
            Assert.AreEqual(ConfigLookupState.PathMissing, Config.Inspect("runtime.config", "/flag/x").State);
            var wrongType = Config.Inspect("summer.event", "/payload/flag");
            Assert.AreEqual(ConfigLookupState.ResourceWrongType, wrongType.State);
            Assert.AreEqual("rev-3", wrongType.RevisionId);
            Assert.IsNull(wrongType.Value);
            Assert.AreEqual(ConfigLookupState.ResourceWrongType, Config.Inspect("zz.future", "/flag").State);
        }

        [Test]
        public void UnavailableReads_ReturnTheCallerDefault()
        {
            Assert.IsTrue(Config.GetBool("runtime.config", "/flag", true));
            Assert.IsNull(Config.GetJson("runtime.config"));
            var none = Config.Inspect("runtime.config");
            Assert.AreEqual(ControlPlaneSource.None, none.Source);
            Assert.IsNull(none.DeploymentId);

            InitializeRuntimeConfig();

            Assert.AreEqual(5L, Config.GetInteger("missing.config", "/count", 5));
            Assert.AreEqual(5L, Config.GetInteger("runtime.config", "/missing", 5));
            Assert.IsFalse(Config.GetBool("summer.event", "/payload/flag", false));
            Assert.IsFalse(Config.GetBool("zz.future", "/flag", false));
            Assert.IsNull(Config.GetString("runtime.config", "/missing", null));
            Assert.IsNull(Config.GetJson("runtime.config", "/missing"));
            Assert.IsNull(Config.GetJson("zz.future"));
        }

        [Test]
        public void ResourceWrongType_LogsOneWarning()
        {
            InitializeRuntimeConfig();

            var warnings = Warnings(() =>
            {
                Config.GetBool("zz.future", "/flag", false);
                Config.Inspect("zz.future", "/flag");
            });

            CollectionAssert.AreEqual(new[] { "[Yes2SDK] ControlPlane.Config ResourceWrongType for zz.future /flag: Resource is future.v9, not config.v1; returning the default" }, warnings);
        }

        [TestCase("flag")]
        [TestCase("#/flag")]
        [TestCase("/~")]
        [TestCase("/~2")]
        [TestCase("/m~n")]
        public void MalformedPointer_IsACallerError(string pointer)
        {
            Assert.Throws<ArgumentException>(() => Config.GetBool("runtime.config", pointer, true));
            Assert.Throws<ArgumentException>(() => Config.Inspect("runtime.config", pointer));
            InitializeRuntimeConfig();
            Assert.Throws<ArgumentException>(() => Config.GetString("runtime.config", pointer, "default"));
            Assert.Throws<ArgumentException>(() => Config.GetJson("runtime.config", pointer));
        }

        [Test]
        public void NullArguments_AreCallerErrors()
        {
            Assert.Throws<ArgumentNullException>(() => Config.GetBool(null, "/flag", true));
            Assert.Throws<ArgumentNullException>(() => Config.GetBool("runtime.config", null, true));
            Assert.Throws<ArgumentNullException>(() => Config.Inspect("runtime.config", null));
        }

        [Test]
        public void GetJson_RootPointerReturnsTheWholePayload()
        {
            InitializeRuntimeConfig();

            Assert.IsTrue(JToken.DeepEquals(JToken.Parse("{\"flag\":false,\"price\":250}"), Config.GetJson("shop.config")));
            Assert.IsTrue(JToken.DeepEquals(JToken.Parse("{\"flag\":false,\"price\":250}"), Config.GetJson("shop.config", "")));
        }

        [Test]
        public void GetJsonAndInspect_ReturnCopiesThatCannotChangeLaterReads()
        {
            InitializeRuntimeConfig();

            var json = (JObject)Config.GetJson("runtime.config", "/nested");
            json["deep"]["leaf"] = "mutated";
            ((JObject)Config.Inspect("runtime.config", "/nested").Value)["deep"] = null;
            ((JObject)Config.GetJson("runtime.config"))["flag"] = false;

            Assert.AreEqual("v", Config.GetString("runtime.config", "/nested/deep/leaf", "default"));
            Assert.IsTrue(Config.GetBool("runtime.config", "/flag", false));
            Assert.AreNotSame(Config.GetJson("runtime.config"), Config.GetJson("runtime.config"));
        }

        [Test]
        public void Lookup_ReportsLkgProvenance()
        {
            var snapshot = SnapshotBytes("rel-1", Resource("runtime.config", "config.v1", "rev-1", "{\"flag\":true}"));
            Serve("dep-1", "rel-1", snapshot);
            Initialize();
            _controlPlane.Shutdown();
            _transport.Routes.Clear();

            var status = Initialize();

            Assert.AreEqual(ControlPlaneSource.Lkg, status.Source);
            var lookup = Config.Inspect("runtime.config", "/flag");
            Assert.AreEqual(ConfigLookupState.Value, lookup.State);
            Assert.AreEqual(ControlPlaneSource.Lkg, lookup.Source);
            Assert.AreEqual("dep-1", lookup.DeploymentId);
            Assert.IsTrue(Config.GetBool("runtime.config", "/flag", false));
        }

        [Test]
        public void DeploymentActivated_SwitchesEveryReadAtOnce()
        {
            Serve("dep-1", "rel-1", SnapshotBytes("rel-1",
                Resource("a.config", "config.v1", "rev-a1", "{\"v\":1}"),
                Resource("b.config", "config.v1", "rev-b1", "{\"v\":1}")));
            Initialize();
            var seen = new List<string>();
            _controlPlane.DeploymentActivated += activation =>
            {
                var lookup = Config.Inspect("a.config", "/v");
                seen.Add(activation.DeploymentId + " " + lookup.DeploymentId + " " + lookup.RevisionId + " " + Config.GetInteger("a.config", "/v", -1) + " " + Config.GetInteger("b.config", "/v", -1));
            };

            Serve("dep-2", "rel-2", SnapshotBytes("rel-2",
                Resource("a.config", "config.v1", "rev-a2", "{\"v\":2}"),
                Resource("b.config", "config.v1", "rev-b2", "{\"v\":2}")));
            Refresh();
            Refresh();

            CollectionAssert.AreEqual(new[] { "dep-2 dep-2 rev-a2 2 2" }, seen);
            Assert.AreEqual(2L, Config.GetInteger("a.config", "/v", -1));
            Assert.AreEqual(2L, Config.GetInteger("b.config", "/v", -1));
        }

        [Test]
        public void ConcurrentReads_NeverSeeATornDeployment()
        {
            var deployments = Enumerable.Range(1, 20).Select(i => SnapshotBytes("rel-" + i, Resource("runtime.config", "config.v1", "rev-" + i, "{\"v\":" + i + "}"))).ToArray();
            Serve("dep-1", "rel-1", deployments[0]);
            Initialize();
            var torn = new List<string>();
            var done = 0;
            var reader = new Thread(() =>
            {
                while (Volatile.Read(ref done) == 0)
                {
                    var lookup = Config.Inspect("runtime.config", "/v");
                    var expected = lookup.DeploymentId.Substring("dep-".Length);
                    if (lookup.ReleaseId != "rel-" + expected || lookup.RevisionId != "rev-" + expected || (string)lookup.Value != expected)
                    {
                        lock (torn)
                        {
                            torn.Add(lookup.DeploymentId + " " + lookup.ReleaseId + " " + lookup.RevisionId + " " + lookup.Value);
                        }
                    }
                }
            });
            reader.Start();
            for (var i = 2; i <= deployments.Length; i++)
            {
                Serve("dep-" + i, "rel-" + i, deployments[i - 1]);
                Refresh();
            }
            Volatile.Write(ref done, 1);
            reader.Join();

            CollectionAssert.IsEmpty(torn);
            Assert.AreEqual(20L, Config.GetInteger("runtime.config", "/v", -1));
        }

        [Test]
        public void ControlPlaneConfig_HasNoEventOrPoller()
        {
            var type = typeof(ControlPlaneConfig);

            CollectionAssert.IsEmpty(type.GetEvents());
            CollectionAssert.AreEquivalent(
                new[] { "GetBool", "GetInteger", "GetNumber", "GetString", "GetJson", "Inspect" },
                type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly).Select(m => m.Name));
        }

        [Test]
        public void ConfigFixtureReads_MatchCore()
        {
            var fixtures = Path.Combine(ControlPlaneContractFixtureTests.Fixtures, "valid", "config-v1");
            var resources = new List<string>();
            var reads = new List<(string Key, JObject Read)>();
            foreach (var expected in Directory.GetFiles(fixtures, "*.expected.json").OrderBy(path => path, StringComparer.Ordinal))
            {
                var outcome = JObject.Parse(File.ReadAllText(expected));
                if (outcome["reads"] == null)
                {
                    continue;
                }
                var key = "fixture." + Path.GetFileName(expected).Replace(".expected.json", "");
                resources.Add(Resource(key, "config.v1", "rev-1", File.ReadAllText(expected.Replace(".expected.json", ".json"))));
                reads.AddRange(outcome["reads"].Select(read => (key, (JObject)read)));
            }
            Serve("dep-1", "rel-1", SnapshotBytes("rel-1", resources.ToArray()));
            Initialize();

            var mismatches = new List<string>();
            foreach (var (key, read) in reads)
            {
                var pointer = (string)read["pointer"];
                var actual = Actual(key, pointer);
                var expected = read["value"] != null ? "value " + Canonical(read["value"]) : (string)read["result"];
                if (actual != expected)
                {
                    mismatches.Add(key + " " + pointer + ": expected " + expected + ", got " + actual);
                }
                if (read["integer"] != null && (Config.GetInteger(key, pointer, long.MinValue) != long.MinValue) != (bool)read["integer"])
                {
                    mismatches.Add(key + " " + pointer + ": integer read expected " + read["integer"]);
                }
            }
            Assert.AreEqual(49, reads.Count);
            CollectionAssert.IsEmpty(mismatches);
        }

        private string Actual(string key, string pointer)
        {
            try
            {
                var lookup = Config.Inspect(key, pointer);
                return lookup.State == ConfigLookupState.Value ? "value " + Canonical(lookup.Value) : lookup.State == ConfigLookupState.PathMissing ? "absent" : lookup.State.ToString();
            }
            catch (ArgumentException)
            {
                return "malformed";
            }
        }

        private static string Canonical(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    return "{" + string.Join(",", ((JObject)token).Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => Canonical(new JValue(p.Name)) + ":" + Canonical(p.Value))) + "}";
                case JTokenType.Array:
                    return "[" + string.Join(",", token.Select(Canonical)) + "]";
                case JTokenType.Integer:
                case JTokenType.Float:
                    return ((double)token).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        private sealed class Transport : IControlPlaneTransport
        {
            public Dictionary<string, byte[]> Routes { get; } = new Dictionary<string, byte[]>();

            public Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
            {
                return Task.FromResult(Routes.TryGetValue(request.Url, out var body)
                    ? ControlPlaneTransportResponse.Completed(200, body)
                    : ControlPlaneTransportResponse.Completed(404, null));
            }
        }
    }
}
