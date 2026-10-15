using Newtonsoft.Json.Linq;

namespace Yes2SDK
{
    /// <summary>Options for <see cref="Yes2SDKControlPlane.InitializeAsync"/>.</summary>
    public sealed class ControlPlaneOptions
    {
        public string GameKey { get; set; }
        public string EnvironmentKey { get; set; }
        public string RuntimeOrigin { get; set; } = Yes2SDKControlPlane.DefaultRuntimeOrigin;
        /// <summary>0 disables automatic refresh; otherwise 15..3600.</summary>
        public int RefreshIntervalSeconds { get; set; } = 60;
    }

    public enum ControlPlaneSource
    {
        None,
        Lkg,
        Remote
    }

    /// <summary>Immutable view of the active control-plane state.</summary>
    public sealed class ControlPlaneStatus
    {
        internal static readonly ControlPlaneStatus None = new ControlPlaneStatus(ControlPlaneSource.None, null, null, null, null, null);

        internal ControlPlaneStatus(ControlPlaneSource source, string gameKey, string environmentKey, string deploymentId, string releaseId, string lastError)
        {
            Source = source;
            GameKey = gameKey;
            EnvironmentKey = environmentKey;
            DeploymentId = deploymentId;
            ReleaseId = releaseId;
            LastError = lastError;
        }

        public bool Available => Source != ControlPlaneSource.None;
        public ControlPlaneSource Source { get; }
        public string GameKey { get; }
        public string EnvironmentKey { get; }
        public string DeploymentId { get; }
        public string ReleaseId { get; }
        public int? ProtocolVersion => Available ? 1 : (int?)null;
        /// <summary>errors-v1 code of the latest failed or degraded operation, or null.</summary>
        public string LastError { get; }
    }

    /// <summary>A Resource of the active Deployment; <see cref="Data"/> returns a fresh copy on every read.</summary>
    public sealed class RuntimeResource
    {
        private readonly JToken _data;

        internal RuntimeResource(string key, string typeKey, string revisionId, JToken data)
        {
            Key = key;
            TypeKey = typeKey;
            RevisionId = revisionId;
            _data = data;
        }

        public string Key { get; }
        public string TypeKey { get; }
        public string RevisionId { get; }
        public JToken Data => _data.DeepClone();
    }

    public sealed class ControlPlaneActivation
    {
        internal ControlPlaneActivation(ControlPlaneStatus status)
        {
            GameKey = status.GameKey;
            EnvironmentKey = status.EnvironmentKey;
            DeploymentId = status.DeploymentId;
            ReleaseId = status.ReleaseId;
            Source = status.Source;
        }

        public string GameKey { get; }
        public string EnvironmentKey { get; }
        public string DeploymentId { get; }
        public string ReleaseId { get; }
        public ControlPlaneSource Source { get; }
    }
}
