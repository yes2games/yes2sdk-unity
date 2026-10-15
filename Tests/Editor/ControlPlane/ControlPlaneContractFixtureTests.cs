using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Yes2SDK.Tests
{
    public class ControlPlaneContractFixtureTests
    {
        private const string Bootstrap = "protocol/bootstrap-v1.schema.json";
        private const string Snapshot = "protocol/runtime-snapshot-v1.schema.json";
        private const string Config = "modules/config-v1.schema.json";
        private const string LiveOpsEvent = "modules/liveops-event-v1.schema.json";
        private const string ExpectedSuffix = ".expected.json";

        private static string Fixtures => Path.Combine(
            PackageInfo.FindForAssembly(typeof(ControlPlaneContractFixtureTests).Assembly).resolvedPath, "ci~", "core-contracts", "fixtures");

        [TestCase(Bootstrap, 98)]
        [TestCase(Snapshot, 102)]
        [TestCase(Config, 56)]
        [TestCase(LiveOpsEvent, 62)]
        public void EveryVendoredFixture_ProducesItsExpectedOutcome(string schema, int count)
        {
            var mismatches = new List<string>();
            var ran = 0;
            foreach (var input in Directory.GetFiles(Fixtures, "*.json", SearchOption.AllDirectories).Where(path => !path.EndsWith(ExpectedSuffix)).OrderBy(path => path, System.StringComparer.Ordinal))
            {
                var expected = JObject.Parse(File.ReadAllText(input.Substring(0, input.Length - ".json".Length) + ExpectedSuffix));
                if ((string)expected["schema"] != schema)
                {
                    continue;
                }
                ran++;
                var outcome = Outcome(schema, File.ReadAllBytes(input), (JObject)expected["binding"]);
                if (outcome != (string)expected["outcome"])
                {
                    mismatches.Add(Path.GetFileName(Path.GetDirectoryName(input)) + "/" + Path.GetFileName(input) + ": expected " + expected["outcome"] + ", got " + outcome);
                }
            }
            Assert.AreEqual(count, ran, "fixture count for " + schema);
            CollectionAssert.IsEmpty(mismatches);
        }

        [TestCase(1048576, "ok", "ok")]
        [TestCase(1048577, "bootstrap_invalid", "snapshot_invalid")]
        public void GeneratedDocumentSizeFixtures_ProduceTheirExpectedOutcome(int bytes, string bootstrapOutcome, string snapshotOutcome)
        {
            var bootstrap = new JObject
            {
                ["protocolVersion"] = 1,
                ["gameKey"] = "nsr",
                ["environmentKey"] = "production",
                ["deploymentId"] = "dep-1",
                ["releaseId"] = "rel-1",
                ["snapshotUrl"] = "https://runtime.yes2games.com/snapshot.json",
                ["snapshotDigest"] = "sha256:" + new string('0', 64),
                ["padding"] = ""
            };
            var snapshot = new JObject
            {
                ["protocolVersion"] = 1,
                ["gameKey"] = "nsr",
                ["releaseId"] = "rel-1",
                ["resources"] = new JArray(),
                ["artifacts"] = new JArray(),
                ["padding"] = ""
            };
            var snapshotBytes = Padded(snapshot, bytes);
            Assert.AreEqual(
                new[] { bootstrapOutcome, snapshotOutcome },
                new[]
                {
                    Outcome(Bootstrap, Padded(bootstrap, bytes), new JObject { ["gameKey"] = "nsr", ["environmentKey"] = "production" }),
                    Outcome(Snapshot, snapshotBytes, new JObject { ["gameKey"] = "nsr", ["releaseId"] = "rel-1", ["snapshotDigest"] = ControlPlaneSha256.Digest(snapshotBytes) })
                });
        }

        private static byte[] Padded(JObject document, int bytes)
        {
            var text = document.ToString(Newtonsoft.Json.Formatting.None) + "\n";
            var padded = text.Replace("\"padding\":\"\"", "\"padding\":\"" + new string('x', bytes - Encoding.UTF8.GetByteCount(text)) + "\"");
            var result = Encoding.UTF8.GetBytes(padded);
            Assert.AreEqual(bytes, result.Length);
            return result;
        }

        private static string Outcome(string schema, byte[] input, JObject binding)
        {
            switch (schema)
            {
                case Bootstrap:
                    return Code(ControlPlaneDocuments.ReadBootstrap(input, (string)binding["gameKey"], (string)binding["environmentKey"]));
                case Snapshot:
                    var bootstrap = new ControlPlaneBootstrap(
                        (string)binding["gameKey"], "production", "dep-1", (string)binding["releaseId"], "https://runtime.yes2games.com/snapshot.json", (string)binding["snapshotDigest"]);
                    return Code(ControlPlaneDocuments.ReadSnapshot(input, bootstrap));
                case Config:
                    return Code(ControlPlaneDocuments.ReadPayload(input, ControlPlaneDocuments.ConfigTypeKey));
                case LiveOpsEvent:
                    return Code(ControlPlaneDocuments.ReadPayload(input, ControlPlaneDocuments.LiveOpsEventTypeKey));
                default:
                    throw new AssertionException("unknown schema " + schema);
            }
        }

        private static string Code<T>(ControlPlaneResult<T> result) where T : class
        {
            return result.Succeeded ? "ok" : result.Error.Value.Code();
        }
    }
}
