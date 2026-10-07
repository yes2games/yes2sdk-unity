using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Yes2SDK
{
    internal sealed class ControlPlaneBootstrap
    {
        public ControlPlaneBootstrap(string gameKey, string environmentKey, string deploymentId, string releaseId, string snapshotUrl, string snapshotDigest)
        {
            GameKey = gameKey;
            EnvironmentKey = environmentKey;
            DeploymentId = deploymentId;
            ReleaseId = releaseId;
            SnapshotUrl = snapshotUrl;
            SnapshotDigest = snapshotDigest;
        }

        public string GameKey { get; }
        public string EnvironmentKey { get; }
        public string DeploymentId { get; }
        public string ReleaseId { get; }
        public string SnapshotUrl { get; }
        public string SnapshotDigest { get; }
    }

    internal sealed class ControlPlaneSnapshot
    {
        public ControlPlaneSnapshot(ControlPlaneBootstrap bootstrap, byte[] bytes, JObject root)
        {
            Bootstrap = bootstrap;
            Bytes = bytes;
            Root = root;
        }

        public ControlPlaneBootstrap Bootstrap { get; }
        public byte[] Bytes { get; }
        public JObject Root { get; }
    }

    internal sealed class ControlPlaneResult<T> where T : class
    {
        private ControlPlaneResult(T value, ControlPlaneErrorCategory? error, string detail)
        {
            Value = value;
            Error = error;
            Detail = detail;
        }

        public T Value { get; }
        public ControlPlaneErrorCategory? Error { get; }
        public string Detail { get; }
        public bool Succeeded => Error == null;

        public static ControlPlaneResult<T> Success(T value)
        {
            return new ControlPlaneResult<T>(value, null, null);
        }

        public static ControlPlaneResult<T> Failure(ControlPlaneErrorCategory error, string detail)
        {
            return new ControlPlaneResult<T>(null, error, detail);
        }
    }

    internal static class ControlPlaneDocuments
    {
        public const int MaxDocumentBytes = 1048576;
        public const long MaxArtifactBytes = 16777216;
        public const int MaxIdLength = 128;

        private static readonly Regex GameKey = new Regex(@"^[a-z0-9][a-z0-9-]{0,62}\z", RegexOptions.CultureInvariant);
        private static readonly Regex EnvironmentKey = new Regex(@"^[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
        private static readonly Regex ResourceKey = new Regex(@"^[a-z0-9][a-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant);
        private static readonly Regex TypeKey = new Regex(@"^[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
        private static readonly Regex MediaType = new Regex(@"^[a-z0-9][a-z0-9!#$&^_.+-]{0,126}/[a-z0-9][a-z0-9!#$&^_.+-]{0,126}\z", RegexOptions.CultureInvariant);

        public static ControlPlaneResult<ControlPlaneBootstrap> ReadBootstrap(byte[] bytes, string gameKey, string environmentKey)
        {
            try
            {
                var root = ParseObject(bytes);
                RequireProtocol(root);
                var bootstrap = new ControlPlaneBootstrap(
                    ReadKey(root, "gameKey", GameKey),
                    ReadKey(root, "environmentKey", EnvironmentKey),
                    ReadId(root, "deploymentId"),
                    ReadId(root, "releaseId"),
                    ReadHttpsUrl(root, "snapshotUrl"),
                    ReadDigest(root, "snapshotDigest"));
                Require(bootstrap.GameKey == gameKey, "gameKey does not match the requested Game");
                Require(bootstrap.EnvironmentKey == environmentKey, "environmentKey does not match the requested Environment");
                return ControlPlaneResult<ControlPlaneBootstrap>.Success(bootstrap);
            }
            catch (Rejection rejection)
            {
                return ControlPlaneResult<ControlPlaneBootstrap>.Failure(
                    rejection.Unsupported ? ControlPlaneErrorCategory.ProtocolUnsupported : ControlPlaneErrorCategory.BootstrapInvalid,
                    rejection.Message);
            }
        }

        public static ControlPlaneResult<ControlPlaneSnapshot> ReadSnapshot(byte[] bytes, ControlPlaneBootstrap bootstrap)
        {
            try
            {
                Require(bytes != null && bytes.Length <= MaxDocumentBytes, "snapshot exceeds " + MaxDocumentBytes + " bytes");
                Require(ControlPlaneSha256.Digest(bytes) == bootstrap.SnapshotDigest, "snapshot digest does not match snapshotDigest");
                var root = ParseObject(bytes);
                RequireProtocol(root);
                Require(ReadKey(root, "gameKey", GameKey) == bootstrap.GameKey, "gameKey does not match the bootstrap");
                Require(ReadId(root, "releaseId") == bootstrap.ReleaseId, "releaseId does not match the bootstrap");
                RequireResources(ReadArray(root, "resources"));
                RequireArtifacts(ReadArray(root, "artifacts"));
                return ControlPlaneResult<ControlPlaneSnapshot>.Success(new ControlPlaneSnapshot(bootstrap, bytes, root));
            }
            catch (Rejection rejection)
            {
                return ControlPlaneResult<ControlPlaneSnapshot>.Failure(
                    rejection.Unsupported ? ControlPlaneErrorCategory.ProtocolUnsupported : ControlPlaneErrorCategory.SnapshotInvalid,
                    rejection.Message);
            }
        }

        private static void RequireResources(JArray resources)
        {
            string previous = null;
            foreach (var token in resources)
            {
                var resource = ReadObject(token, "resources");
                var key = ReadKey(resource, "key", ResourceKey);
                ReadKey(resource, "typeKey", TypeKey);
                ReadId(resource, "revisionId");
                Require(resource.ContainsKey("data"), "resource " + key + " has no data");
                RequireAscending(previous, key, "resource keys");
                previous = key;
            }
        }

        private static void RequireArtifacts(JArray artifacts)
        {
            string previous = null;
            foreach (var token in artifacts)
            {
                var artifact = ReadObject(token, "artifacts");
                var digest = ReadDigest(artifact, "digest");
                ReadKey(artifact, "mediaType", MediaType);
                if (!artifact.TryGetValue("size", out var size) || !ControlPlaneStrictJson.TryGetSafeInteger(size, out var bytes) || bytes < 0 || bytes > MaxArtifactBytes)
                {
                    throw Invalid("artifact " + digest + " size is not an integer in 0.." + MaxArtifactBytes);
                }
                ReadHttpsUrl(artifact, "url");
                RequireAscending(previous, digest, "artifact digests");
                previous = digest;
            }
        }

        private static void RequireAscending(string previous, string current, string what)
        {
            if (previous == null)
            {
                return;
            }
            var order = string.CompareOrdinal(previous, current);
            Require(order != 0, "duplicate " + what);
            Require(order < 0, what + " are not in ascending order");
        }

        private static JObject ParseObject(byte[] bytes)
        {
            Require(bytes != null && bytes.Length <= MaxDocumentBytes, "document exceeds " + MaxDocumentBytes + " bytes");
            JToken root;
            try
            {
                root = ControlPlaneStrictJson.Parse(bytes);
            }
            catch (FormatException e)
            {
                throw Invalid(e.Message);
            }
            return ReadObject(root, "document");
        }

        private static void RequireProtocol(JObject root)
        {
            if (!root.TryGetValue("protocolVersion", out var token) || !ControlPlaneStrictJson.TryGetSafeInteger(token, out var version))
            {
                throw Invalid("protocolVersion is not an integer");
            }
            if (version != 1)
            {
                throw new Rejection("protocolVersion " + version + " is not supported", true);
            }
        }

        private static JObject ReadObject(JToken token, string what)
        {
            Require(token != null && token.Type == JTokenType.Object, what + " entry is not an object");
            return (JObject)token;
        }

        private static JArray ReadArray(JObject parent, string name)
        {
            if (!parent.TryGetValue(name, out var token) || token.Type != JTokenType.Array)
            {
                throw Invalid(name + " is not an array");
            }
            return (JArray)token;
        }

        private static string ReadString(JObject parent, string name)
        {
            if (!parent.TryGetValue(name, out var token) || token.Type != JTokenType.String)
            {
                throw Invalid(name + " is not a string");
            }
            return (string)token;
        }

        private static string ReadKey(JObject parent, string name, Regex pattern)
        {
            var value = ReadString(parent, name);
            Require(pattern.IsMatch(value), name + " is malformed");
            return value;
        }

        private static string ReadId(JObject parent, string name)
        {
            var value = ReadString(parent, name);
            Require(value.Length >= 1 && value.Length <= MaxIdLength, name + " is not 1.." + MaxIdLength + " bytes");
            foreach (var c in value)
            {
                Require(c <= '\u007f', name + " is not ASCII");
            }
            return value;
        }

        private static string ReadHttpsUrl(JObject parent, string name)
        {
            var value = ReadString(parent, name);
            Require(
                value.Trim().Length == value.Length && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0,
                name + " is not an absolute HTTPS URL");
            return value;
        }

        private static string ReadDigest(JObject parent, string name)
        {
            var value = ReadString(parent, name);
            Require(ControlPlaneSha256.IsDigest(value), name + " is not a sha256 digest");
            return value;
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw Invalid(reason);
            }
        }

        private static Exception Invalid(string reason)
        {
            return new Rejection(reason, false);
        }

        private sealed class Rejection : Exception
        {
            public Rejection(string message, bool unsupported) : base(message)
            {
                Unsupported = unsupported;
            }

            public bool Unsupported { get; }
        }
    }
}
