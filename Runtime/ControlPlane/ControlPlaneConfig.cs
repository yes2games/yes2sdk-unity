using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Yes2SDK
{
    public enum ConfigLookupState
    {
        Value,
        NoActiveControlPlane,
        ResourceMissing,
        ResourceWrongType,
        PathMissing,
        TypeMismatch
    }

    /// <summary>Outcome and provenance of one Config read; <see cref="Value"/> is a caller-owned copy.</summary>
    public sealed class ConfigLookup
    {
        internal ConfigLookup(ConfigLookupState state, ControlPlaneStatus status, string resourceKey, string pointer, string revisionId, JToken value)
        {
            State = state;
            Source = status?.Source ?? ControlPlaneSource.None;
            ResourceKey = resourceKey;
            Pointer = pointer;
            RevisionId = revisionId;
            DeploymentId = status?.DeploymentId;
            ReleaseId = status?.ReleaseId;
            Value = value;
        }

        public ConfigLookupState State { get; }
        public ControlPlaneSource Source { get; }
        public string ResourceKey { get; }
        public string Pointer { get; }
        public string RevisionId { get; }
        public string DeploymentId { get; }
        public string ReleaseId { get; }
        public JToken Value { get; }
    }

    internal sealed class ControlPlaneActiveState
    {
        public ControlPlaneActiveState(ControlPlaneStatus status, Dictionary<string, JObject> resources)
        {
            Status = status;
            Resources = resources;
        }

        public ControlPlaneStatus Status { get; }
        public Dictionary<string, JObject> Resources { get; }
    }

    /// <summary>Typed reads of config.v1 Resources by RFC 6901 pointer; every miss returns the caller's default.</summary>
    public sealed class ControlPlaneConfig
    {
        private const int MaxDiagnostics = 64;
        private static readonly Regex ArrayIndex = new Regex(@"^(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant);

        private readonly Func<ControlPlaneActiveState> _active;
        private readonly HashSet<string> _diagnosed = new HashSet<string>(StringComparer.Ordinal);

        internal ControlPlaneConfig(Func<ControlPlaneActiveState> active)
        {
            _active = active;
        }

        public bool GetBool(string resourceKey, string pointer, bool defaultValue)
        {
            var token = Typed(resourceKey, pointer, "boolean", t => t.Type == JTokenType.Boolean);
            return token == null ? defaultValue : (bool)token;
        }

        /// <summary>Accepts only integral numbers within ±(2^53−1), including integral doubles such as 1.0.</summary>
        public long GetInteger(string resourceKey, string pointer, long defaultValue)
        {
            var token = Typed(resourceKey, pointer, "safe integer", t => ControlPlaneStrictJson.TryGetSafeInteger(t, out _));
            return token != null && ControlPlaneStrictJson.TryGetSafeInteger(token, out var value) ? value : defaultValue;
        }

        public double GetNumber(string resourceKey, string pointer, double defaultValue)
        {
            var token = Typed(resourceKey, pointer, "number", t => t.Type == JTokenType.Integer || t.Type == JTokenType.Float);
            return token == null ? defaultValue : (double)token;
        }

        public string GetString(string resourceKey, string pointer, string defaultValue)
        {
            var token = Typed(resourceKey, pointer, "string", t => t.Type == JTokenType.String);
            return token == null ? defaultValue : (string)token;
        }

        /// <summary>Returns a caller-owned copy of the value, or null when it is unavailable.</summary>
        public JToken GetJson(string resourceKey, string pointer = "")
        {
            return Find(resourceKey, pointer, out _, out _, out _)?.DeepClone();
        }

        public ConfigLookup Inspect(string resourceKey, string pointer = "")
        {
            var token = Find(resourceKey, pointer, out var state, out var status, out var revisionId);
            return new ConfigLookup(state, status, resourceKey, pointer, revisionId, token?.DeepClone());
        }

        private JToken Typed(string resourceKey, string pointer, string expected, Func<JToken, bool> accepts)
        {
            var token = Find(resourceKey, pointer, out _, out _, out _);
            if (token == null || accepts(token))
            {
                return token;
            }
            Diagnose(ConfigLookupState.TypeMismatch, resourceKey, pointer, "is " + token.Type.ToString().ToLowerInvariant() + ", not " + expected);
            return null;
        }

        private JToken Find(string resourceKey, string pointer, out ConfigLookupState state, out ControlPlaneStatus status, out string revisionId)
        {
            if (resourceKey == null)
            {
                throw new ArgumentNullException(nameof(resourceKey));
            }
            var tokens = ParsePointer(pointer);
            var active = _active();
            status = active?.Status;
            revisionId = null;
            if (status == null || !status.Available)
            {
                state = ConfigLookupState.NoActiveControlPlane;
                return null;
            }
            if (!active.Resources.TryGetValue(resourceKey, out var resource))
            {
                state = ConfigLookupState.ResourceMissing;
                return null;
            }
            revisionId = (string)resource["revisionId"];
            var typeKey = (string)resource["typeKey"];
            if (typeKey != ControlPlaneDocuments.ConfigTypeKey)
            {
                state = ConfigLookupState.ResourceWrongType;
                Diagnose(state, resourceKey, pointer, "Resource is " + typeKey + ", not " + ControlPlaneDocuments.ConfigTypeKey);
                return null;
            }
            var node = resource["data"];
            foreach (var token in tokens)
            {
                node = Child(node, token);
                if (node == null)
                {
                    state = ConfigLookupState.PathMissing;
                    return null;
                }
            }
            state = ConfigLookupState.Value;
            return node;
        }

        private static JToken Child(JToken node, string token)
        {
            if (node is JObject obj)
            {
                return obj.TryGetValue(token, out var value) ? value : null;
            }
            if (node is JArray array && ArrayIndex.IsMatch(token) && int.TryParse(token, out var index) && index < array.Count)
            {
                return array[index];
            }
            return null;
        }

        private static string[] ParsePointer(string pointer)
        {
            if (pointer == null)
            {
                throw new ArgumentNullException(nameof(pointer));
            }
            if (pointer.Length == 0)
            {
                return Array.Empty<string>();
            }
            if (pointer[0] != '/')
            {
                throw new ArgumentException("JSON Pointer must be empty or start with '/'", nameof(pointer));
            }
            for (var i = pointer.IndexOf('~'); i >= 0; i = pointer.IndexOf('~', i + 1))
            {
                if (i + 1 == pointer.Length || (pointer[i + 1] != '0' && pointer[i + 1] != '1'))
                {
                    throw new ArgumentException("JSON Pointer has a '~' not followed by '0' or '1'", nameof(pointer));
                }
            }
            var tokens = pointer.Substring(1).Split('/');
            for (var i = 0; i < tokens.Length; i++)
            {
                tokens[i] = tokens[i].Replace("~1", "/").Replace("~0", "~");
            }
            return tokens;
        }

        private void Diagnose(ConfigLookupState state, string resourceKey, string pointer, string detail)
        {
            var message = "[Yes2SDK] ControlPlane.Config " + state + " for " + resourceKey + " " + pointer + ": " + detail + "; returning the default";
            lock (_diagnosed)
            {
                if (_diagnosed.Count >= MaxDiagnostics || !_diagnosed.Add(message))
                {
                    return;
                }
            }
            Debug.LogWarning(message);
        }
    }
}
