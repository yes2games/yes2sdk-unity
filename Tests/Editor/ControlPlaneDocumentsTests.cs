using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    internal static class ControlPlaneFixture
    {
        public const string Game = "nsr";
        public const string Environment = "production";
        public const string BootstrapUrl = "https://runtime.yes2games.com/environments/nsr/production/bootstrap-v1.json";
        public const string SnapshotUrl = "https://runtime.yes2games.com/releases/rel-1/runtime-snapshot-v1.json";
        public const string DigestA = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public const string DigestB = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public static JObject Snapshot()
        {
            return new JObject
            {
                ["protocolVersion"] = 1,
                ["gameKey"] = Game,
                ["releaseId"] = "rel-1",
                ["resources"] = new JArray
                {
                    new JObject { ["key"] = "runtime.config", ["typeKey"] = "config.v1", ["revisionId"] = "rev-1", ["data"] = new JObject { ["speed"] = 2 } },
                    new JObject { ["key"] = "zz.future", ["typeKey"] = "future.v9", ["revisionId"] = "rev-2", ["data"] = new JArray(1, 2), ["addedLater"] = true }
                },
                ["artifacts"] = new JArray
                {
                    new JObject { ["digest"] = DigestA, ["mediaType"] = "image/png", ["size"] = 1234, ["url"] = "https://runtime.yes2games.com/blobs/a" },
                    new JObject { ["digest"] = DigestB, ["mediaType"] = "application/octet-stream", ["size"] = 0, ["url"] = "https://runtime.yes2games.com/blobs/b" }
                }
            };
        }

        public static JObject Bootstrap(byte[] snapshot)
        {
            return new JObject
            {
                ["protocolVersion"] = 1,
                ["gameKey"] = Game,
                ["environmentKey"] = Environment,
                ["deploymentId"] = "dep-1",
                ["releaseId"] = "rel-1",
                ["snapshotUrl"] = SnapshotUrl,
                ["snapshotDigest"] = ControlPlaneSha256.Digest(snapshot)
            };
        }

        public static byte[] Bytes(JToken token)
        {
            return Encoding.UTF8.GetBytes(token.ToString(Formatting.None));
        }

        public static ControlPlaneBootstrap BootstrapFor(byte[] snapshot)
        {
            return ControlPlaneDocuments.ReadBootstrap(Bytes(Bootstrap(snapshot)), Game, Environment).Value;
        }

        public static string Code(ControlPlaneErrorCategory? error)
        {
            return error?.Code();
        }
    }

    public class ControlPlaneDocumentsTests
    {
        private static readonly byte[] SnapshotBytes = ControlPlaneFixture.Bytes(ControlPlaneFixture.Snapshot());

        private static ControlPlaneResult<ControlPlaneBootstrap> ReadBootstrap(Action<JObject> mutate)
        {
            var bootstrap = ControlPlaneFixture.Bootstrap(SnapshotBytes);
            mutate(bootstrap);
            return ControlPlaneDocuments.ReadBootstrap(ControlPlaneFixture.Bytes(bootstrap), ControlPlaneFixture.Game, ControlPlaneFixture.Environment);
        }

        private static ControlPlaneResult<ControlPlaneSnapshot> ReadSnapshot(Action<JObject> mutate)
        {
            var snapshot = ControlPlaneFixture.Snapshot();
            mutate(snapshot);
            var bytes = ControlPlaneFixture.Bytes(snapshot);
            return ControlPlaneDocuments.ReadSnapshot(bytes, ControlPlaneFixture.BootstrapFor(bytes));
        }

        private static ControlPlaneResult<ControlPlaneSnapshot> ReadSnapshotText(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            return ControlPlaneDocuments.ReadSnapshot(bytes, ControlPlaneFixture.BootstrapFor(bytes));
        }

        [Test]
        public void ReadBootstrap_ReadsEveryField()
        {
            var result = ReadBootstrap(_ => { });

            Assert.IsTrue(result.Succeeded, result.Detail);
            Assert.AreEqual("nsr", result.Value.GameKey);
            Assert.AreEqual("production", result.Value.EnvironmentKey);
            Assert.AreEqual("dep-1", result.Value.DeploymentId);
            Assert.AreEqual("rel-1", result.Value.ReleaseId);
            Assert.AreEqual(ControlPlaneFixture.SnapshotUrl, result.Value.SnapshotUrl);
            Assert.AreEqual(ControlPlaneSha256.Digest(SnapshotBytes), result.Value.SnapshotDigest);
        }

        [Test]
        public void ReadBootstrap_IgnoresUnknownFields()
        {
            Assert.IsTrue(ReadBootstrap(b => b["addedLater"] = new JObject { ["x"] = 1 }).Succeeded);
        }

        [Test]
        public void ReadBootstrap_AcceptsIntegralFloatProtocolVersion()
        {
            Assert.IsTrue(ReadBootstrap(b => b["protocolVersion"] = 1.0).Succeeded);
        }

        [TestCase("protocolVersion")]
        [TestCase("gameKey")]
        [TestCase("environmentKey")]
        [TestCase("deploymentId")]
        [TestCase("releaseId")]
        [TestCase("snapshotUrl")]
        [TestCase("snapshotDigest")]
        public void ReadBootstrap_RejectsMissingField(string field)
        {
            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(ReadBootstrap(b => b.Remove(field)).Error));
        }

        [TestCase("protocolVersion", "\"1\"")]
        [TestCase("protocolVersion", "1.5")]
        [TestCase("protocolVersion", "null")]
        [TestCase("gameKey", "\"other\"")]
        [TestCase("gameKey", "\"NSR\"")]
        [TestCase("gameKey", "\"nsr\\n\"")]
        [TestCase("gameKey", "1")]
        [TestCase("environmentKey", "\"staging\"")]
        [TestCase("environmentKey", "\"production\\n\"")]
        [TestCase("deploymentId", "\"\"")]
        [TestCase("deploymentId", "\"dép\"")]
        [TestCase("deploymentId", "7")]
        [TestCase("releaseId", "\"\"")]
        [TestCase("snapshotUrl", "\"http://runtime.yes2games.com/s.json\"")]
        [TestCase("snapshotUrl", "\"runtime.yes2games.com/s.json\"")]
        [TestCase("snapshotUrl", "\"/releases/s.json\"")]
        [TestCase("snapshotUrl", "\" https://runtime.yes2games.com/s.json\"")]
        [TestCase("snapshotDigest", "\"sha256:E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855\"")]
        [TestCase("snapshotDigest", "\"sha512:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\"")]
        public void ReadBootstrap_RejectsMalformedField(string field, string json)
        {
            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(ReadBootstrap(b => b[field] = JToken.Parse(json)).Error));
        }

        [Test]
        public void ReadBootstrap_BoundsOpaqueIdsTo128AsciiBytes()
        {
            Assert.IsTrue(ReadBootstrap(b => b["deploymentId"] = new string('d', 128)).Succeeded);
            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(ReadBootstrap(b => b["deploymentId"] = new string('d', 129)).Error));
        }

        [TestCase(2)]
        [TestCase(0)]
        [TestCase(-1)]
        public void ReadBootstrap_RejectsOtherProtocolVersionsAsUnsupported(int version)
        {
            Assert.AreEqual("protocol_unsupported", ControlPlaneFixture.Code(ReadBootstrap(b => b["protocolVersion"] = version).Error));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{\"protocolVersion\":1,\"protocolVersion\":1}")]
        [TestCase("{\"protocolVersion\":1,}")]
        public void ReadBootstrap_RejectsInvalidJson(string json)
        {
            var result = ControlPlaneDocuments.ReadBootstrap(Encoding.UTF8.GetBytes(json), ControlPlaneFixture.Game, ControlPlaneFixture.Environment);

            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(result.Error));
        }

        [Test]
        public void ReadBootstrap_RejectsDuplicateKeyThatWouldOverrideIdentity()
        {
            var json = Encoding.UTF8.GetString(ControlPlaneFixture.Bytes(ControlPlaneFixture.Bootstrap(SnapshotBytes))).TrimEnd('}') + ",\"gameKey\":\"nsr\"}";

            var result = ControlPlaneDocuments.ReadBootstrap(Encoding.UTF8.GetBytes(json), ControlPlaneFixture.Game, ControlPlaneFixture.Environment);

            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(result.Error));
        }

        [Test]
        public void ReadBootstrap_RejectsDocumentOverOneMebibyte()
        {
            var json = Encoding.UTF8.GetString(ControlPlaneFixture.Bytes(ControlPlaneFixture.Bootstrap(SnapshotBytes)));
            var bytes = Encoding.UTF8.GetBytes(json.PadRight(ControlPlaneDocuments.MaxDocumentBytes + 1));

            var result = ControlPlaneDocuments.ReadBootstrap(bytes, ControlPlaneFixture.Game, ControlPlaneFixture.Environment);

            Assert.AreEqual("bootstrap_invalid", ControlPlaneFixture.Code(result.Error));
        }

        [Test]
        public void ReadSnapshot_KeepsExactBytesAndUnknownTypes()
        {
            var bootstrap = ControlPlaneFixture.BootstrapFor(SnapshotBytes);

            var result = ControlPlaneDocuments.ReadSnapshot(SnapshotBytes, bootstrap);

            Assert.IsTrue(result.Succeeded, result.Detail);
            Assert.AreSame(SnapshotBytes, result.Value.Bytes);
            Assert.AreSame(bootstrap, result.Value.Bootstrap);
            Assert.AreEqual("future.v9", (string)result.Value.Root["resources"][1]["typeKey"]);
        }

        [Test]
        public void ReadSnapshot_RejectsDigestMismatchBeforeParsing()
        {
            var bootstrap = ControlPlaneFixture.BootstrapFor(SnapshotBytes);
            var unsupported = ControlPlaneFixture.Snapshot();
            unsupported["protocolVersion"] = 2;

            var result = ControlPlaneDocuments.ReadSnapshot(ControlPlaneFixture.Bytes(unsupported), bootstrap);

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(result.Error));
            StringAssert.Contains("digest", result.Detail);
        }

        [Test]
        public void ReadSnapshot_ReportsDigestBeforeDuplicateKey()
        {
            var bootstrap = ControlPlaneFixture.BootstrapFor(SnapshotBytes);
            var duplicate = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(SnapshotBytes).Replace("\"speed\":2", "\"speed\":2,\"speed\":3"));

            var result = ControlPlaneDocuments.ReadSnapshot(duplicate, bootstrap);

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(result.Error));
            StringAssert.Contains("digest", result.Detail);
        }

        [Test]
        public void ReadSnapshot_RejectsSingleByteChange()
        {
            var bootstrap = ControlPlaneFixture.BootstrapFor(SnapshotBytes);
            var changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(SnapshotBytes).Replace("\"speed\":2", "\"speed\":3"));

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ControlPlaneDocuments.ReadSnapshot(changed, bootstrap).Error));
        }

        [Test]
        public void ReadSnapshot_AcceptsExactlyOneMebibyte()
        {
            var json = Encoding.UTF8.GetString(SnapshotBytes).PadRight(ControlPlaneDocuments.MaxDocumentBytes);

            Assert.IsTrue(ReadSnapshotText(json).Succeeded);
        }

        [Test]
        public void ReadSnapshot_RejectsOneByteOverOneMebibyte()
        {
            var json = Encoding.UTF8.GetString(SnapshotBytes).PadRight(ControlPlaneDocuments.MaxDocumentBytes + 1);

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshotText(json).Error));
        }

        [TestCase(2)]
        [TestCase(0)]
        public void ReadSnapshot_RejectsOtherProtocolVersionsAsUnsupported(int version)
        {
            Assert.AreEqual("protocol_unsupported", ControlPlaneFixture.Code(ReadSnapshot(s => s["protocolVersion"] = version).Error));
        }

        [Test]
        public void ReadSnapshot_RejectsDuplicateJsonKey()
        {
            var json = Encoding.UTF8.GetString(SnapshotBytes).Replace("\"speed\":2", "\"speed\":2,\"speed\":3");

            var result = ReadSnapshotText(json);

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(result.Error));
            StringAssert.DoesNotContain("digest", result.Detail);
        }

        [Test]
        public void ReadSnapshot_RejectsNonFiniteNumber()
        {
            var json = Encoding.UTF8.GetString(SnapshotBytes).Replace("\"speed\":2", "\"speed\":1e400");

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshotText(json).Error));
        }

        [Test]
        public void ReadSnapshot_IgnoresUnknownFields()
        {
            Assert.IsTrue(ReadSnapshot(s =>
            {
                s["addedLater"] = 1;
                s["artifacts"][0]["addedLater"] = "x";
            }).Succeeded);
        }

        [Test]
        public void ReadSnapshot_AcceptsEmptyResourcesAndArtifacts()
        {
            Assert.IsTrue(ReadSnapshot(s =>
            {
                s["resources"] = new JArray();
                s["artifacts"] = new JArray();
            }).Succeeded);
        }

        [Test]
        public void ReadSnapshot_RejectsGameOtherThanBootstrap()
        {
            var snapshot = ControlPlaneFixture.Snapshot();
            snapshot["gameKey"] = "other";
            var bytes = ControlPlaneFixture.Bytes(snapshot);
            var bootstrap = ControlPlaneFixture.BootstrapFor(bytes);

            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ControlPlaneDocuments.ReadSnapshot(bytes, bootstrap).Error));
        }

        [Test]
        public void ReadSnapshot_RejectsReleaseOtherThanBootstrap()
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => s["releaseId"] = "rel-2").Error));
        }

        [TestCase("protocolVersion")]
        [TestCase("gameKey")]
        [TestCase("releaseId")]
        [TestCase("resources")]
        [TestCase("artifacts")]
        public void ReadSnapshot_RejectsMissingTopLevelField(string field)
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => s.Remove(field)).Error));
        }

        [TestCase("key")]
        [TestCase("typeKey")]
        [TestCase("revisionId")]
        [TestCase("data")]
        public void ReadSnapshot_RejectsResourceMissingField(string field)
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => ((JObject)s["resources"][0]).Remove(field)).Error));
        }

        [TestCase("key", "\"Runtime.config\"")]
        [TestCase("key", "\".config\"")]
        [TestCase("typeKey", "\"Config.v1\"")]
        [TestCase("revisionId", "\"\"")]
        public void ReadSnapshot_RejectsMalformedResourceField(string field, string json)
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => s["resources"][0][field] = JToken.Parse(json)).Error));
        }

        [Test]
        public void ReadSnapshot_RejectsNonObjectResource()
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => ((JArray)s["resources"]).Add("runtime.other")).Error));
        }

        [Test]
        public void ReadSnapshot_RejectsUnsortedResources()
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => s["resources"][1]["key"] = "a.first").Error));
        }

        [Test]
        public void ReadSnapshot_RejectsDuplicateResourceKey()
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => s["resources"][1]["key"] = "runtime.config").Error));
        }

        [TestCase("digest")]
        [TestCase("mediaType")]
        [TestCase("size")]
        [TestCase("url")]
        public void ReadSnapshot_RejectsArtifactMissingField(string field)
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => ((JObject)s["artifacts"][0]).Remove(field)).Error));
        }

        [TestCase("digest", "\"sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"")]
        [TestCase("mediaType", "\"image/PNG\"")]
        [TestCase("mediaType", "\"image/png; charset=binary\"")]
        [TestCase("mediaType", "\"png\"")]
        [TestCase("size", "16777217")]
        [TestCase("size", "-1")]
        [TestCase("size", "1.5")]
        [TestCase("size", "\"1234\"")]
        [TestCase("size", "1e30")]
        [TestCase("url", "\"http://runtime.yes2games.com/blobs/a\"")]
        public void ReadSnapshot_RejectsMalformedArtifactField(string field, string json)
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => s["artifacts"][0][field] = JToken.Parse(json)).Error));
        }

        [TestCase("16777216")]
        [TestCase("1234.0")]
        public void ReadSnapshot_AcceptsArtifactSizeUpToSixteenMebibytes(string json)
        {
            Assert.IsTrue(ReadSnapshot(s => s["artifacts"][0]["size"] = JToken.Parse(json)).Succeeded);
        }

        [Test]
        public void ReadSnapshot_RejectsUnsortedArtifacts()
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s =>
            {
                s["artifacts"][0]["digest"] = ControlPlaneFixture.DigestB;
                s["artifacts"][1]["digest"] = ControlPlaneFixture.DigestA;
            }).Error));
        }

        [Test]
        public void ReadSnapshot_RejectsDuplicateArtifactDigest()
        {
            Assert.AreEqual("snapshot_invalid", ControlPlaneFixture.Code(ReadSnapshot(s => s["artifacts"][1]["digest"] = ControlPlaneFixture.DigestA).Error));
        }
    }
}
