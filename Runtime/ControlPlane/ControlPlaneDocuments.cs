using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
        public const int MaxPayloadBytes = 131072;
        public const int MaxConfigDepth = 16;
        public const int MaxConfigMembers = 1024;
        public const int MaxConfigKeyBytes = 128;
        public const int MaxConfigStringBytes = 65536;
        public const int MaxArtifactRefs = 64;
        public const string ConfigTypeKey = "config.v1";
        public const string LiveOpsEventTypeKey = "liveops.event.v1";

        private const int ResourceDataDepth = 4;

        private static readonly Regex GameKey = new Regex(@"^[a-z0-9][a-z0-9-]{0,62}\z", RegexOptions.CultureInvariant);
        private static readonly Regex EnvironmentKey = new Regex(@"^[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
        private static readonly Regex ResourceKey = new Regex(@"^[a-z0-9][a-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant);
        private static readonly Regex TypeKey = new Regex(@"^[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
        private static readonly Regex MediaType = new Regex(@"^[a-z0-9][a-z0-9!#$&^_.+-]{0,126}/[a-z0-9][a-z0-9!#$&^_.+-]{0,126}\z", RegexOptions.CultureInvariant);
        private static readonly Regex Timestamp = new Regex(@"^(?!0000)([0-9]{4})-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9])(?:\.([0-9]{1,3}))?Z\z", RegexOptions.CultureInvariant);

        public static bool IsGameKey(string value)
        {
            return value != null && GameKey.IsMatch(value);
        }

        public static bool IsEnvironmentKey(string value)
        {
            return value != null && EnvironmentKey.IsMatch(value);
        }

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
                var root = ReadObject(Parse(bytes, ResourceDataDepth), "document");
                RequireProtocol(root);
                Require(ReadKey(root, "gameKey", GameKey) == bootstrap.GameKey, "gameKey does not match the bootstrap");
                Require(ReadId(root, "releaseId") == bootstrap.ReleaseId, "releaseId does not match the bootstrap");
                RequireResources(ReadArray(root, "resources"), RequireArtifacts(ReadArray(root, "artifacts")));
                return ControlPlaneResult<ControlPlaneSnapshot>.Success(new ControlPlaneSnapshot(bootstrap, bytes, root));
            }
            catch (Rejection rejection)
            {
                return ControlPlaneResult<ControlPlaneSnapshot>.Failure(
                    rejection.Unsupported ? ControlPlaneErrorCategory.ProtocolUnsupported : ControlPlaneErrorCategory.SnapshotInvalid,
                    rejection.Message);
            }
        }

        public static ControlPlaneResult<JToken> ReadPayload(byte[] bytes, string typeKey)
        {
            try
            {
                Require(bytes != null && bytes.Length <= MaxPayloadBytes, "payload exceeds " + MaxPayloadBytes + " bytes");
                var data = Parse(bytes, 1);
                RequirePayload(typeKey, data, null);
                return ControlPlaneResult<JToken>.Success(data);
            }
            catch (Rejection rejection)
            {
                return ControlPlaneResult<JToken>.Failure(ControlPlaneErrorCategory.SnapshotInvalid, rejection.Message);
            }
        }

        private static void RequireResources(JArray resources, HashSet<string> artifacts)
        {
            string previous = null;
            foreach (var token in resources)
            {
                var resource = ReadObject(token, "resources");
                var key = ReadKey(resource, "key", ResourceKey);
                var typeKey = ReadKey(resource, "typeKey", TypeKey);
                ReadId(resource, "revisionId");
                Require(resource.TryGetValue("data", out var data), "resource " + key + " has no data");
                if (typeKey == ConfigTypeKey || typeKey == LiveOpsEventTypeKey)
                {
                    Require(ControlPlaneStrictJson.MeasureOf(data).Bytes <= MaxPayloadBytes, "resource " + key + " data exceeds " + MaxPayloadBytes + " bytes");
                    RequirePayload(typeKey, data, artifacts);
                }
                RequireAscending(previous, key, "resource keys");
                previous = key;
            }
        }

        private static HashSet<string> RequireArtifacts(JArray artifacts)
        {
            var digests = new HashSet<string>(StringComparer.Ordinal);
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
                digests.Add(digest);
            }
            return digests;
        }

        private static void RequirePayload(string typeKey, JToken data, HashSet<string> artifacts)
        {
            Require(!ControlPlaneStrictJson.MeasureOf(data).LoneSurrogate, typeKey + " data holds an unpaired surrogate escape");
            if (typeKey == ConfigTypeKey)
            {
                RequireConfig(data, typeKey);
                return;
            }
            Require(typeKey == LiveOpsEventTypeKey, typeKey + " is not a known typeKey");
            var liveOpsEvent = ReadObject(data, typeKey);
            Require(liveOpsEvent.Count == 4 && liveOpsEvent.ContainsKey("startAt") && liveOpsEvent.ContainsKey("endAt") && liveOpsEvent.ContainsKey("payload") && liveOpsEvent.ContainsKey("artifactRefs"),
                typeKey + " must hold exactly startAt, endAt, payload and artifactRefs");
            Require(ReadInstant(liveOpsEvent, "startAt") < ReadInstant(liveOpsEvent, "endAt"), "startAt is not before endAt");
            RequireConfig(liveOpsEvent["payload"], "payload");
            var refs = ReadArray(liveOpsEvent, "artifactRefs");
            Require(refs.Count <= MaxArtifactRefs, "artifactRefs holds more than " + MaxArtifactRefs + " digests");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in refs)
            {
                Require(token.Type == JTokenType.String && ControlPlaneSha256.IsDigest((string)token), "artifactRefs entry is not a sha256 digest");
                Require(seen.Add((string)token), "duplicate artifactRefs digest");
                Require(artifacts == null || artifacts.Contains((string)token), "artifactRefs digest " + token + " is not a snapshot artifact");
            }
        }

        private static long ReadInstant(JObject parent, string name)
        {
            var match = Timestamp.Match(ReadString(parent, name));
            Require(match.Success, name + " is not an RFC 3339 UTC timestamp");
            int Field(int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
            Require(Field(3) <= DateTime.DaysInMonth(Field(1), Field(2)), name + " is not a calendar date");
            var milliseconds = match.Groups[7].Success ? int.Parse(match.Groups[7].Value.PadRight(3, '0'), CultureInfo.InvariantCulture) : 0;
            return new DateTime(Field(1), Field(2), Field(3), Field(4), Field(5), Field(6), DateTimeKind.Utc).Ticks + milliseconds * TimeSpan.TicksPerMillisecond;
        }

        private static void RequireConfig(JToken payload, string what)
        {
            Require(payload.Type == JTokenType.Object, what + " is not an object");
            RequireConfigValue(payload, 1);
        }

        private static void RequireConfigValue(JToken token, int depth)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    Require(depth <= MaxConfigDepth, "config nesting deeper than " + MaxConfigDepth);
                    Require(obj.Count <= MaxConfigMembers, "config object has more than " + MaxConfigMembers + " properties");
                    foreach (var property in obj.Properties())
                    {
                        RequireText(property.Name, MaxConfigKeyBytes, "config key");
                        RequireConfigValue(property.Value, depth + 1);
                    }
                    break;
                case JTokenType.Array:
                    var array = (JArray)token;
                    Require(depth <= MaxConfigDepth, "config nesting deeper than " + MaxConfigDepth);
                    Require(array.Count <= MaxConfigMembers, "config array has more than " + MaxConfigMembers + " items");
                    foreach (var item in array)
                    {
                        RequireConfigValue(item, depth + 1);
                    }
                    break;
                case JTokenType.String:
                    RequireText((string)token, MaxConfigStringBytes, "config string");
                    break;
                case JTokenType.Boolean:
                case JTokenType.Integer:
                case JTokenType.Float:
                    break;
                default:
                    throw Invalid("config value is " + token.Type.ToString().ToLowerInvariant());
            }
        }

        private static void RequireText(string value, int maxBytes, string what)
        {
            Require(Encoding.UTF8.GetByteCount(value) <= maxBytes, what + " exceeds " + maxBytes + " UTF-8 bytes");
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
            return ReadObject(Parse(bytes, 0), "document");
        }

        private static JToken Parse(byte[] bytes, int measureDepth)
        {
            try
            {
                return ControlPlaneStrictJson.Parse(bytes, measureDepth);
            }
            catch (FormatException e)
            {
                throw Invalid(e.Message);
            }
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
                value.All(c => c > ' ' && c <= '~') && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0,
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
