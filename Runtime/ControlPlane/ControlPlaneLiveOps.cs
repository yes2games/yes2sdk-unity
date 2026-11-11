using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yes2SDK
{
    public sealed class LiveOpsEvent
    {
        internal LiveOpsEvent(JObject resource, ControlPlaneBootstrap bootstrap)
        {
            var data = (JObject)resource["data"];
            ResourceKey = (string)resource["key"];
            RevisionId = (string)resource["revisionId"];
            StartAt = new DateTimeOffset(ControlPlaneDocuments.ReadInstant(data, "startAt"), TimeSpan.Zero);
            EndAt = new DateTimeOffset(ControlPlaneDocuments.ReadInstant(data, "endAt"), TimeSpan.Zero);
            PayloadJson = data["payload"].ToString(Formatting.None);
            ArtifactRefs = Array.AsReadOnly(data["artifactRefs"].Select(digest => (string)digest).ToArray());
            DeploymentId = bootstrap.DeploymentId;
            ReleaseId = bootstrap.ReleaseId;
        }

        public string ResourceKey { get; }
        public string RevisionId { get; }
        public DateTimeOffset StartAt { get; }
        public DateTimeOffset EndAt { get; }
        public string PayloadJson { get; }
        public IReadOnlyList<string> ArtifactRefs { get; }
        public string DeploymentId { get; }
        public string ReleaseId { get; }

        internal bool IsActiveAt(DateTimeOffset now)
        {
            return StartAt <= now && now < EndAt;
        }
    }

    public sealed class ControlPlaneLiveOps
    {
        private static readonly IReadOnlyList<LiveOpsEvent> None = Array.AsReadOnly(new LiveOpsEvent[0]);

        private readonly Func<ControlPlaneActiveState> _active;

        internal ControlPlaneLiveOps(Func<ControlPlaneActiveState> active)
        {
            _active = active;
        }

        internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

        public IReadOnlyList<LiveOpsEvent> GetEvents()
        {
            return _active()?.Events ?? None;
        }

        public IReadOnlyList<LiveOpsEvent> GetActiveEvents()
        {
            var now = Clock();
            return Array.AsReadOnly(GetEvents().Where(liveOpsEvent => liveOpsEvent.IsActiveAt(now)).ToArray());
        }

        public bool TryGetEvent(string resourceKey, out LiveOpsEvent liveOpsEvent)
        {
            liveOpsEvent = GetEvents().FirstOrDefault(candidate => string.Equals(candidate.ResourceKey, resourceKey, StringComparison.Ordinal));
            return liveOpsEvent != null;
        }

        internal static IReadOnlyList<LiveOpsEvent> Read(ControlPlaneSnapshot snapshot)
        {
            return Array.AsReadOnly(snapshot.Root["resources"]
                .Cast<JObject>()
                .Where(resource => (string)resource["typeKey"] == ControlPlaneDocuments.LiveOpsEventTypeKey)
                .Select(resource => new LiveOpsEvent(resource, snapshot.Bootstrap))
                .OrderBy(liveOpsEvent => liveOpsEvent.ResourceKey, StringComparer.Ordinal)
                .ToArray());
        }
    }
}
