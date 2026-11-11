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
    public class ControlPlaneArtifactTests
    {
        private const string SnapshotUrl = "https://runtime.yes2games.com/releases/{0}/runtime-snapshot-v1.json";
        private const string FixtureRelease = "rel_01j9z3k5w8e2r4t6y8u0i2o4p6";
        private const string FixtureWinter = "sha256:b0940a72356dfccd36a77a007ad31ef4709155d5bfa4aef325f229b1700dca2e";

        private string _root;
        private Transport _transport;
        private Yes2SDKControlPlane _controlPlane;
        private SynchronizationContext _context;
        private readonly List<string> _descriptors = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _root = Path.Combine(Path.GetTempPath(), "yes2sdk-cp-artifacts-" + Guid.NewGuid().ToString("N"));
            _transport = new Transport();
            _descriptors.Clear();
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

        private string ArtifactDirectory => Path.Combine(_root, "artifacts");

        private Yes2SDKControlPlane NewControlPlane()
        {
            return new Yes2SDKControlPlane(
                _transport,
                (delay, token) => Task.CompletedTask,
                new System.Random(0),
                (game, environment) => new ControlPlaneLkgJournal(Path.Combine(_root, "journal"), game, environment),
                game => new ControlPlaneArtifactCache(ArtifactDirectory));
        }

        private static string UrlOf(string digest)
        {
            return "https://runtime.yes2games.com/artifacts/" + digest.Substring("sha256:".Length);
        }

        private string Publish(byte[] bytes, string mediaType, long? size = null)
        {
            var digest = ControlPlaneSha256.Digest(bytes);
            _descriptors.Add("{\"digest\":\"" + digest + "\",\"mediaType\":\"" + mediaType + "\",\"size\":" + (size ?? bytes.Length) + ",\"url\":\"" + UrlOf(digest) + "\"}");
            _transport.Routes[UrlOf(digest)] = bytes;
            return digest;
        }

        private ControlPlaneStatus Activate(string deploymentId = "dep-1")
        {
            var releaseId = "rel-" + deploymentId;
            var ordered = _descriptors.OrderBy(d => (string)JObject.Parse(d)["digest"], StringComparer.Ordinal);
            var snapshot = Encoding.UTF8.GetBytes(
                "{\"protocolVersion\":1,\"gameKey\":\"nsr\",\"releaseId\":\"" + releaseId + "\",\"resources\":[],\"artifacts\":[" + string.Join(",", ordered) + "]}");
            Serve(deploymentId, releaseId, snapshot);
            var status = _controlPlane.Status.Available ? Refresh() : Initialize();
            Assert.AreEqual(deploymentId, status.DeploymentId, status.LastError);
            return status;
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

        private VerifiedArtifact Get(string digest, CancellationToken token = default)
        {
            return _controlPlane.GetArtifactAsync(digest, token).GetAwaiter().GetResult();
        }

        private string Failure(Func<Task> call)
        {
            return Assert.Throws<ControlPlaneException>(() => call().GetAwaiter().GetResult()).Code;
        }

        private string[] CacheFiles()
        {
            return Directory.Exists(ArtifactDirectory) ? Directory.GetFiles(ArtifactDirectory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray() : new string[0];
        }

        private int ArtifactRequests(string digest)
        {
            return _transport.Requests.Count(request => request.Url == UrlOf(digest));
        }

        private static byte[] Png(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static byte[] Jpeg(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                return texture.EncodeToJPG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static int LiveTextures()
        {
            return Resources.FindObjectsOfTypeAll<Texture2D>().Length;
        }

        [Test]
        public void Artifact_IsDownloadedLazilyVerifiedAndCachedByDigest()
        {
            var bytes = Encoding.UTF8.GetBytes("hello artifact");
            var digest = Publish(bytes, "text/plain");
            Activate();
            Assert.AreEqual(0, ArtifactRequests(digest));

            var artifact = Get(digest);

            Assert.AreEqual(digest, artifact.Digest);
            Assert.AreEqual("text/plain", artifact.MediaType);
            Assert.AreEqual(bytes.Length, artifact.Size);
            Assert.AreEqual(Path.Combine(ArtifactDirectory, digest.Substring(7)), artifact.FilePath);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(artifact.FilePath));
            CollectionAssert.AreEqual(new[] { digest.Substring(7) }, CacheFiles());
            var request = _transport.Requests.Single(r => r.Url == UrlOf(digest));
            Assert.AreEqual(TimeSpan.FromSeconds(15), request.Timeout);
            Assert.AreEqual(bytes.Length, request.MaxBytes);
            Assert.IsFalse(request.BypassCache);
            StringAssert.EndsWith(".tmp", request.FilePath);

            Assert.AreEqual(artifact.FilePath, Get(digest).FilePath);
            Assert.AreEqual(1, ArtifactRequests(digest));
        }

        [Test]
        public void CorruptDigestFixture_IsRejectedWithArtifactIntegrityFailed()
        {
            Serve("dep-1", FixtureRelease, File.ReadAllBytes(Path.Combine(ControlPlaneContractFixtureTests.Fixtures, "valid", "runtime-snapshot-v1", "liveops-events.json")));
            Assert.AreEqual(ControlPlaneSource.Remote, Initialize().Source);
            _transport.Routes["https://runtime.yes2games.com/artifacts/sha256/" + FixtureWinter.Substring(7)] = new byte[2048];

            Assert.AreEqual("artifact_integrity_failed", Failure(() => _controlPlane.GetArtifactAsync(FixtureWinter, CancellationToken.None)));
            CollectionAssert.IsEmpty(CacheFiles());
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(0)]
        public void WrongBytes_AreRejectedAndNeverCached(int sizeDelta)
        {
            var good = Encoding.UTF8.GetBytes("verified");
            var goodDigest = Publish(good, "text/plain");
            var bytes = Encoding.UTF8.GetBytes("payload!");
            var digest = Publish(bytes, "text/plain");
            Activate();
            var cached = Get(goodDigest).FilePath;
            _transport.Routes[UrlOf(digest)] = sizeDelta == 0 ? Encoding.UTF8.GetBytes("tampered") : new byte[bytes.Length + sizeDelta];

            Assert.AreEqual("artifact_integrity_failed", Failure(() => _controlPlane.GetArtifactAsync(digest, CancellationToken.None)));

            CollectionAssert.AreEqual(new[] { goodDigest.Substring(7) }, CacheFiles());
            CollectionAssert.AreEqual(good, File.ReadAllBytes(cached));
            Assert.AreEqual(1, ArtifactRequests(digest));
        }

        [Test]
        public void TamperedCacheFile_IsDownloadedAgain()
        {
            var bytes = Encoding.UTF8.GetBytes("original");
            var digest = Publish(bytes, "text/plain");
            Activate();
            var path = Get(digest).FilePath;
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes("tampere"));

            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Get(digest).FilePath));
            Assert.AreEqual(2, ArtifactRequests(digest));

            File.WriteAllBytes(path, Encoding.UTF8.GetBytes("tampered"));
            _transport.Routes[UrlOf(digest)] = Encoding.UTF8.GetBytes("tampered");
            Assert.AreEqual("artifact_integrity_failed", Failure(() => _controlPlane.GetArtifactAsync(digest, CancellationToken.None)));
        }

        [Test]
        public void UnknownDigest_IsArtifactMissingAndMalformedDigestIsACallerError()
        {
            var digest = Publish(new byte[] { 1 }, "application/octet-stream");

            Assert.AreEqual("artifact_missing", Failure(() => _controlPlane.GetArtifactAsync(digest, CancellationToken.None)));
            Activate();
            Assert.AreEqual("artifact_missing", Failure(() => _controlPlane.GetArtifactAsync(ControlPlaneFixture.DigestA, CancellationToken.None)));
            Assert.Throws<ArgumentException>(() => _controlPlane.GetArtifactAsync(null, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => _controlPlane.GetArtifactAsync(digest.ToUpperInvariant(), CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => _controlPlane.GetArtifactAsync(digest.Substring(7), CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(0, ArtifactRequests(digest));
        }

        [Test]
        public void ZeroByteArtifact_IsVerified()
        {
            var digest = Publish(new byte[0], "application/octet-stream");
            Activate();

            Assert.AreEqual(0, new FileInfo(Get(digest).FilePath).Length);
        }

        [TestCase(404, 1, "artifact_http_failed")]
        [TestCase(403, 1, "artifact_http_failed")]
        [TestCase(503, 2, "artifact_http_failed")]
        [TestCase(429, 2, "artifact_http_failed")]
        [TestCase(-1, 2, "network_unavailable")]
        [TestCase(-2, 2, "timeout")]
        public void TransportFailures_RetryOnlyTransientErrors(int status, int attempts, string code)
        {
            var digest = Publish(new byte[] { 1, 2, 3 }, "application/octet-stream");
            Activate();
            _transport.Fail[UrlOf(digest)] = status == -1 ? ControlPlaneTransportResponse.NetworkUnavailable("down")
                : status == -2 ? ControlPlaneTransportResponse.TimedOut()
                : ControlPlaneTransportResponse.Completed(status, null);

            Assert.AreEqual(code, Failure(() => _controlPlane.GetArtifactAsync(digest, CancellationToken.None)));
            Assert.AreEqual(attempts, ArtifactRequests(digest));
            CollectionAssert.IsEmpty(CacheFiles());
        }

        [Test]
        public void Cancellation_DeletesTheTempFileAndKeepsTheCache()
        {
            var good = Encoding.UTF8.GetBytes("verified");
            var goodDigest = Publish(good, "text/plain");
            var digest = Publish(new byte[64], "application/octet-stream");
            Activate();
            Get(goodDigest);
            var hold = _transport.Hold(UrlOf(digest), 10);
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = _controlPlane.GetArtifactAsync(digest, cancellation.Token);
                Assert.IsFalse(pending.IsCompleted);
                Assert.IsTrue(File.Exists(hold.Request.FilePath));
                Assert.IsFalse(File.Exists(Path.Combine(ArtifactDirectory, digest.Substring(7))));

                cancellation.Cancel();

                Assert.Catch<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            }
            CollectionAssert.AreEqual(new[] { goodDigest.Substring(7) }, CacheFiles());
            Assert.IsTrue(_controlPlane.GetArtifactAsync(digest, new CancellationToken(true)).IsCanceled);
        }

        [Test]
        public void Shutdown_CancelsAnOutstandingDownload()
        {
            var digest = Publish(new byte[64], "application/octet-stream");
            Activate();
            _transport.Hold(UrlOf(digest), 10);
            var pending = _controlPlane.GetArtifactAsync(digest, CancellationToken.None);

            _controlPlane.Shutdown();

            Assert.Catch<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            CollectionAssert.IsEmpty(CacheFiles());
        }

        [Test]
        public void KillMidDownload_LeavesTheLkgAndVerifiedCacheIntact()
        {
            var good = Encoding.UTF8.GetBytes("verified");
            var goodDigest = Publish(good, "text/plain");
            var digest = Publish(new byte[64], "application/octet-stream");
            Activate();
            Get(goodDigest);
            var hold = _transport.Hold(UrlOf(digest), 10);
            var abandoned = _controlPlane.GetArtifactAsync(digest, CancellationToken.None);
            Assert.IsFalse(abandoned.IsCompleted);
            var partial = hold.Request.FilePath;
            Assert.AreEqual(10, new FileInfo(partial).Length);

            _transport.Routes.Clear();
            _transport.Fail.Clear();
            _controlPlane = NewControlPlane();
            var status = Initialize();

            Assert.AreEqual(ControlPlaneSource.Lkg, status.Source);
            Assert.AreEqual("dep-1", status.DeploymentId);
            Assert.AreEqual("network_unavailable", status.LastError);
            var cached = Get(goodDigest);
            CollectionAssert.AreEqual(good, File.ReadAllBytes(cached.FilePath));
            Assert.AreEqual(1, ArtifactRequests(goodDigest));
            Assert.IsTrue(File.Exists(partial));
            Assert.AreEqual("network_unavailable", Failure(() => _controlPlane.GetArtifactAsync(digest, CancellationToken.None)));
            Assert.IsFalse(File.Exists(partial));
            CollectionAssert.AreEqual(new[] { goodDigest.Substring(7) }, CacheFiles());
        }

        [Test]
        public void LkgDeployment_ResolvesCachedArtifactsOffline()
        {
            var bytes = Encoding.UTF8.GetBytes("offline");
            var digest = Publish(bytes, "application/json");
            Activate();
            Get(digest);
            _controlPlane.Shutdown();
            _transport.Routes.Clear();
            _controlPlane = NewControlPlane();

            Assert.AreEqual(ControlPlaneSource.Lkg, Initialize().Source);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Get(digest).FilePath));
            Assert.AreEqual(1, ArtifactRequests(digest));
        }

        [Test]
        public void Eviction_KeepsTheBudgetAndNeverDeletesAReturnedFile()
        {
            var first = Publish(Encoding.UTF8.GetBytes("first"), "text/plain");
            var second = Publish(Encoding.UTF8.GetBytes("second"), "text/plain");
            Activate();
            var firstPath = Get(first).FilePath;
            File.SetLastWriteTimeUtc(firstPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var stale = Enumerable.Range(1, 4).Select(i => new string((char)('0' + i), 64)).ToArray();
            for (var i = 0; i < stale.Length; i++)
            {
                var path = Path.Combine(ArtifactDirectory, stale[i]);
                using (var stream = File.Create(path))
                {
                    stream.SetLength(16 * 1024 * 1024);
                }
                File.SetLastWriteTimeUtc(path, new DateTime(2001 + i, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            }
            File.WriteAllText(Path.Combine(ArtifactDirectory, "unrelated.txt"), "kept");

            var secondPath = Get(second).FilePath;

            CollectionAssert.AreEquivalent(new[] { first.Substring(7), second.Substring(7), stale[1], stale[2], stale[3], "unrelated.txt" }, CacheFiles());
            Assert.IsTrue(File.Exists(firstPath));
            Assert.IsTrue(File.Exists(secondPath));
            Assert.LessOrEqual(Directory.GetFiles(ArtifactDirectory).Where(p => Path.GetFileName(p).Length == 64).Sum(p => new FileInfo(p).Length), ControlPlaneArtifactCache.BudgetBytes);
        }

        [Test]
        public void ClearCache_RemovesArtifactsButKeepsTheActiveDeployment()
        {
            var digest = Publish(Encoding.UTF8.GetBytes("clear me"), "text/plain");
            Activate();
            Get(digest);
            File.WriteAllText(Path.Combine(ArtifactDirectory, "unrelated.txt"), "kept");

            _controlPlane.ClearCacheAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "unrelated.txt" }, CacheFiles());
            Assert.AreEqual("dep-1", _controlPlane.Status.DeploymentId);
            Get(digest);
            Assert.AreEqual(2, ArtifactRequests(digest));
        }

        [Test]
        public void ConcurrentRequests_ForOneDigestBothSucceed()
        {
            var bytes = Encoding.UTF8.GetBytes("shared");
            var digest = Publish(bytes, "text/plain");
            Activate();
            var first = _transport.Hold(UrlOf(digest), 0);
            var a = _controlPlane.GetArtifactAsync(digest, CancellationToken.None);
            var firstRequest = first.Request;
            var second = _transport.Hold(UrlOf(digest), 0);
            var b = _controlPlane.GetArtifactAsync(digest, CancellationToken.None);

            File.WriteAllBytes(second.Request.FilePath, bytes);
            second.Pending.SetResult(ControlPlaneTransportResponse.Completed(200, null));
            File.WriteAllBytes(firstRequest.FilePath, bytes);
            first.Pending.SetResult(ControlPlaneTransportResponse.Completed(200, null));

            Assert.AreEqual(a.GetAwaiter().GetResult().FilePath, b.GetAwaiter().GetResult().FilePath);
            CollectionAssert.AreEqual(new[] { digest.Substring(7) }, CacheFiles());
        }

        [TestCase("text/plain", "plain héllo 😀\n")]
        [TestCase("application/json", "{ \"b\": 1,\n  \"a\": [true] }")]
        [TestCase("text/plain", "")]
        public void LoadText_ReturnsStrictUtf8Verbatim(string mediaType, string text)
        {
            var digest = Publish(Encoding.UTF8.GetBytes(text), mediaType);
            Activate();

            Assert.AreEqual(text, _controlPlane.Content.LoadTextAsync(digest, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void LoadText_RejectsInvalidUtf8OversizeAndOtherMediaTypes()
        {
            var invalid = Publish(new byte[] { 0x61, 0xC3, 0x28 }, "text/plain");
            var oversize = Publish(Encoding.ASCII.GetBytes(new string('x', 4 * 1024 * 1024 + 1)), "text/plain");
            var limit = Publish(Encoding.ASCII.GetBytes(new string('y', 4 * 1024 * 1024)), "application/json");
            var image = Publish(Png(1, 1), "image/png");
            Activate();
            var content = _controlPlane.Content;

            Assert.Throws<InvalidDataException>(() => content.LoadTextAsync(invalid, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<InvalidDataException>(() => content.LoadTextAsync(oversize, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(4 * 1024 * 1024, content.LoadTextAsync(limit, CancellationToken.None).GetAwaiter().GetResult().Length);
            Assert.Throws<NotSupportedException>(() => content.LoadTextAsync(image, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<NotSupportedException>(() => content.LoadTextureAsync(limit, CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsTrue(File.Exists(Path.Combine(ArtifactDirectory, invalid.Substring(7))));
        }

        [Test]
        public void LoadTexture_DecodesPngAndJpegIntoIndependentCallerOwnedTextures()
        {
            var png = Publish(Png(3, 2), "image/png");
            var jpeg = Publish(Jpeg(4, 5), "image/jpeg");
            Activate();
            var content = _controlPlane.Content;

            var a = content.LoadTextureAsync(png, CancellationToken.None).GetAwaiter().GetResult();
            var b = content.LoadTextureAsync(png, CancellationToken.None).GetAwaiter().GetResult();
            var c = content.LoadTextureAsync(jpeg, CancellationToken.None).GetAwaiter().GetResult();
            try
            {
                Assert.AreNotSame(a, b);
                Assert.AreEqual(new Vector2Int(3, 2), new Vector2Int(a.width, a.height));
                Assert.AreEqual(new Vector2Int(3, 2), new Vector2Int(b.width, b.height));
                Assert.AreEqual(new Vector2Int(4, 5), new Vector2Int(c.width, c.height));
                Assert.AreEqual(1, ArtifactRequests(png));
                UnityEngine.Object.DestroyImmediate(a);
                Assert.IsTrue(b != null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(b);
                UnityEngine.Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void LoadTexture_RejectsCorruptAndOversizeImagesWithoutLeakingTextures()
        {
            var corrupt = Publish(Encoding.ASCII.GetBytes("not a png at all"), "image/png");
            var wide = Publish(Png(4097, 1), "image/png");
            var edge = Publish(Png(4096, 1), "image/png");
            var webp = Publish(new byte[] { 1, 2, 3 }, "image/webp");
            Activate();
            var content = _controlPlane.Content;
            var before = LiveTextures();

            Assert.Throws<InvalidDataException>(() => content.LoadTextureAsync(corrupt, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<InvalidDataException>(() => content.LoadTextureAsync(wide, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<NotSupportedException>(() => content.LoadTextureAsync(webp, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(before, LiveTextures());
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("not a png at all"), File.ReadAllBytes(_controlPlane.GetArtifactAsync(corrupt, CancellationToken.None).GetAwaiter().GetResult().FilePath));
            var texture = content.LoadTextureAsync(edge, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(4096, texture.width);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void Content_SeesIntegrityFailureBeforeAnyDecode()
        {
            var digest = Publish(Png(2, 2), "image/png");
            Activate();
            _transport.Routes[UrlOf(digest)] = Png(2, 2).Select(b => (byte)~b).ToArray();
            var before = LiveTextures();

            Assert.AreEqual("artifact_integrity_failed", Failure(() => _controlPlane.Content.LoadTextureAsync(digest, CancellationToken.None)));
            Assert.AreEqual("artifact_integrity_failed", Failure(() => _controlPlane.Content.LoadTextAsync(digest, CancellationToken.None)));
            Assert.AreEqual(before, LiveTextures());
        }

        [Test]
        public void LoadTexture_CancelledDuringFetchLeavesNoTextureOrTempFile()
        {
            var digest = Publish(Png(2, 2), "image/png");
            Activate();
            _transport.Hold(UrlOf(digest), 4);
            var before = LiveTextures();
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = _controlPlane.Content.LoadTextureAsync(digest, cancellation.Token);
                cancellation.Cancel();
                Assert.Catch<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            }

            Assert.AreEqual(before, LiveTextures());
            CollectionAssert.IsEmpty(CacheFiles());
        }

        [Test]
        public void PublicSurface_IsExactlyTheLockedMembers()
        {
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;

            CollectionAssert.AreEquivalent(new[] { "LoadTextAsync", "LoadTextureAsync" }, typeof(ControlPlaneContent).GetMethods(flags).Select(m => m.Name));
            CollectionAssert.AreEquivalent(new[] { "Digest", "MediaType", "Size", "FilePath" }, typeof(VerifiedArtifact).GetProperties(flags).Select(p => p.Name));
            Assert.AreSame(Yes2ControlPlane.Instance.Content, Yes2SDK.ControlPlane.Content);
        }

        private sealed class Transport : IControlPlaneTransport
        {
            private readonly Dictionary<string, Queue<Held>> _held = new Dictionary<string, Queue<Held>>();

            public Dictionary<string, byte[]> Routes { get; } = new Dictionary<string, byte[]>();
            public Dictionary<string, ControlPlaneTransportResponse> Fail { get; } = new Dictionary<string, ControlPlaneTransportResponse>();
            public List<ControlPlaneTransportRequest> Requests { get; } = new List<ControlPlaneTransportRequest>();

            public Held Hold(string url, int partialBytes)
            {
                var held = new Held(partialBytes);
                if (!_held.TryGetValue(url, out var queue))
                {
                    _held[url] = queue = new Queue<Held>();
                }
                queue.Enqueue(held);
                return held;
            }

            public Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
            {
                Requests.Add(request);
                if (_held.TryGetValue(request.Url, out var queue) && queue.Count > 0)
                {
                    var held = queue.Dequeue();
                    held.Request = request;
                    File.WriteAllBytes(request.FilePath, new byte[held.PartialBytes]);
                    token.Register(() =>
                    {
                        File.Delete(request.FilePath);
                        held.Pending.TrySetCanceled();
                    });
                    return held.Pending.Task;
                }
                if (Fail.TryGetValue(request.Url, out var failure))
                {
                    return Task.FromResult(failure);
                }
                if (!Routes.TryGetValue(request.Url, out var body))
                {
                    return Task.FromResult(ControlPlaneTransportResponse.NetworkUnavailable("no route"));
                }
                if (request.FilePath == null)
                {
                    return Task.FromResult(ControlPlaneTransportResponse.Completed(200, body));
                }
                if (body.Length > request.MaxBytes)
                {
                    return Task.FromResult(ControlPlaneTransportResponse.TooLarge(200));
                }
                File.WriteAllBytes(request.FilePath, body);
                return Task.FromResult(ControlPlaneTransportResponse.Completed(200, null));
            }
        }

        private sealed class Held
        {
            public Held(int partialBytes)
            {
                PartialBytes = partialBytes;
            }

            public int PartialBytes { get; }
            public ControlPlaneTransportRequest Request { get; set; }
            public TaskCompletionSource<ControlPlaneTransportResponse> Pending { get; } = new TaskCompletionSource<ControlPlaneTransportResponse>();
        }
    }
}
