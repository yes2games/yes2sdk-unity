using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>The control-plane runtime on every platform; the same instance as <c>Yes2SDK.ControlPlane</c>.</summary>
    public static class Yes2ControlPlane
    {
        public static Yes2SDKControlPlane Instance { get; } = new Yes2SDKControlPlane(
            new UnityWebRequestControlPlaneTransport(UnityWebRequestControlPlaneTransport.MonotonicClock()),
            UnityWebRequestControlPlaneTransport.DelayAsync,
            new System.Random(),
            OpenJournal);

        internal static ControlPlaneLkgJournal OpenJournal(string gameKey, string environmentKey)
        {
            return new ControlPlaneLkgJournal(
                Path.Combine(Application.persistentDataPath, "Yes2SDK", "control-plane", "v1", gameKey, environmentKey),
                gameKey,
                environmentKey);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetEditorState()
        {
            Instance.Reset();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ObserveApplication()
        {
            var go = new GameObject("Yes2ControlPlane");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ControlPlaneApplicationEvents>();
        }
    }

    /// <summary>Native control-plane runtime (yes2games/yes2sdk-unity#98).</summary>
    public sealed class Yes2SDKControlPlane
    {
        public const string DefaultRuntimeOrigin = "https://runtime.yes2games.com";

        private static readonly ControlPlaneStatus Superseded = new ControlPlaneStatus(ControlPlaneSource.None, null, null, null, null, ControlPlaneErrorCategory.Cancelled.Code());

        private readonly IControlPlaneTransport _transport;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly System.Random _random;
        private readonly Func<string, string, ControlPlaneLkgJournal> _openJournal;
        private Session _session;
        private bool _paused;
        private TaskCompletionSource<bool> _foreground = Foreground();

        internal Yes2SDKControlPlane(
            IControlPlaneTransport transport,
            Func<TimeSpan, CancellationToken, Task> delay,
            System.Random random,
            Func<string, string, ControlPlaneLkgJournal> openJournal)
        {
            _transport = transport;
            _delay = delay;
            _random = random;
            _openJournal = openJournal;
            Config = new ControlPlaneConfig(() => _session?.Current);
        }

        /// <summary>Typed reads of the active Deployment's config.v1 Resources.</summary>
        public ControlPlaneConfig Config { get; }

        /// <summary>Raised once each time a different deploymentId becomes active.</summary>
        public event Action<ControlPlaneActivation> DeploymentActivated;

        public ControlPlaneStatus Status => _session?.Status ?? ControlPlaneStatus.None;

        /// <summary>Activates the last-known-good Deployment, then the remote one; failures are reported in the returned status.</summary>
        public Task<ControlPlaneStatus> InitializeAsync(ControlPlaneOptions options, CancellationToken token)
        {
            var context = new Context(options);
            var active = _session;
            if (active != null)
            {
                if (!active.Context.Equals(context))
                {
                    throw new InvalidOperationException("ControlPlane is initialized for different options; call Shutdown() first");
                }
                return !active.Started || active.Refreshing?.IsCompleted == true ? Task.FromResult(active.Status) : Refresh(active, token);
            }

            var session = new Session(context, _openJournal(context.GameKey, context.EnvironmentKey), new ControlPlaneFetcher(_transport, _delay, _random));
            _session = session;
            return InitializeSessionAsync(session, token);
        }

        /// <summary>Fetches the bootstrap and activates a new Deployment; concurrent calls share one refresh, and a token cancels only its own wait.</summary>
        public Task<ControlPlaneStatus> RefreshAsync(CancellationToken token)
        {
            var session = _session ?? throw new InvalidOperationException("Call InitializeAsync before RefreshAsync");
            return Refresh(session, token);
        }

        public bool TryGetResource(string resourceKey, out RuntimeResource resource)
        {
            resource = null;
            var resources = _session?.Current.Resources;
            if (resourceKey == null || resources == null || !resources.TryGetValue(resourceKey, out var entry))
            {
                return false;
            }
            resource = new RuntimeResource(resourceKey, (string)entry["typeKey"], (string)entry["revisionId"], entry["data"]);
            return true;
        }

        /// <summary>Deletes the persisted last-known-good Deployment; the active Deployment stays active.</summary>
        public Task ClearCacheAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }
            try
            {
                _session?.Journal.Clear();
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
        }

        /// <summary>Cancels outstanding requests and forgets the active Deployment.</summary>
        public void Shutdown()
        {
            var session = _session;
            _session = null;
            session?.Cancellation.Cancel();
        }

        internal void Reset()
        {
            Shutdown();
            DeploymentActivated = null;
            SetApplicationPaused(false);
        }

        internal void SetApplicationPaused(bool paused)
        {
            _paused = paused;
            if (!paused)
            {
                _foreground.TrySetResult(true);
            }
            else if (_foreground.Task.IsCompleted)
            {
                _foreground = new TaskCompletionSource<bool>();
            }
        }

        private static TaskCompletionSource<bool> Foreground()
        {
            var foreground = new TaskCompletionSource<bool>();
            foreground.SetResult(true);
            return foreground;
        }

        private async Task ScheduleAsync(Session session)
        {
            var token = session.Cancellation.Token;
            var interval = TimeSpan.FromSeconds(session.Context.RefreshIntervalSeconds);
            try
            {
                while (true)
                {
                    await _delay(TimeSpan.FromTicks((long)(interval.Ticks * (0.9 + 0.2 * _random.NextDouble()))), token);
                    while (!_foreground.Task.IsCompleted)
                    {
                        var cancelled = new TaskCompletionSource<bool>();
                        using (token.Register(() => cancelled.TrySetResult(true)))
                        {
                            await Task.WhenAny(_foreground.Task, cancelled.Task);
                        }
                        token.ThrowIfCancellationRequested();
                    }
                    await Refresh(session, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async Task<ControlPlaneStatus> InitializeSessionAsync(Session session, CancellationToken token)
        {
            ControlPlaneSnapshot lkg = null;
            try
            {
                lkg = session.Journal.Load();
            }
            catch (Exception e)
            {
                CacheUnavailable(session, e);
            }
            if (lkg != null)
            {
                session.Persisted = true;
                Activate(session, lkg, ControlPlaneSource.Lkg, null);
            }
            session.Started = true;
            var refresh = Refresh(session, token);
            if (session.Context.RefreshIntervalSeconds > 0)
            {
                _ = ScheduleAsync(session);
            }
            return await refresh;
        }

        private async Task<ControlPlaneStatus> Refresh(Session session, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return CallerCancelled(session);
            }
            if (session.Refreshing == null || session.Refreshing.IsCompleted)
            {
                session.Refreshing = RefreshSessionAsync(session);
            }
            var shared = session.Refreshing;
            if (shared.IsCompleted || !token.CanBeCanceled)
            {
                return await shared;
            }

            var cancelled = new TaskCompletionSource<bool>();
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(shared, cancelled.Task) == shared)
                {
                    return await shared;
                }
            }
            return CallerCancelled(session);
        }

        private ControlPlaneStatus CallerCancelled(Session session)
        {
            if (session != _session)
            {
                return Superseded;
            }
            var status = session.Status;
            return new ControlPlaneStatus(status.Source, status.GameKey, status.EnvironmentKey, status.DeploymentId, status.ReleaseId, ControlPlaneErrorCategory.Cancelled.Code());
        }

        private async Task<ControlPlaneStatus> RefreshSessionAsync(Session session)
        {
            var token = session.Cancellation.Token;
            var bootstrap = await session.Fetcher.FetchBootstrapAsync(session.Context.BootstrapUrl, session.Context.GameKey, session.Context.EnvironmentKey, token);
            if (!bootstrap.Succeeded)
            {
                return Fail(session, bootstrap.Error.Value);
            }

            var active = session.Active?.Bootstrap;
            var candidate = bootstrap.Value;
            if (active != null && active.DeploymentId == candidate.DeploymentId && active.ReleaseId == candidate.ReleaseId && active.SnapshotDigest == candidate.SnapshotDigest)
            {
                return Activate(session, session.Active, ControlPlaneSource.Remote, session.Persisted ? null : Persist(session, session.Active));
            }

            var snapshot = await session.Fetcher.FetchSnapshotAsync(candidate, token);
            if (!snapshot.Succeeded)
            {
                return Fail(session, snapshot.Error.Value);
            }
            return Activate(session, snapshot.Value, ControlPlaneSource.Remote, Persist(session, snapshot.Value));
        }

        private string Persist(Session session, ControlPlaneSnapshot snapshot)
        {
            if (session != _session)
            {
                return null;
            }
            try
            {
                session.Journal.Write(snapshot);
                session.Persisted = true;
                return null;
            }
            catch (Exception e)
            {
                session.Persisted = false;
                return CacheUnavailable(session, e);
            }
        }

        private static string CacheUnavailable(Session session, Exception e)
        {
            Debug.LogWarning("[Yes2SDK] ControlPlane cache unavailable for " + session.Context.GameKey + "/" + session.Context.EnvironmentKey + ": " + e.GetType().Name + ": " + e.Message);
            session.SetStatus(session.Source, ControlPlaneErrorCategory.CacheUnavailable.Code());
            return session.Status.LastError;
        }

        private ControlPlaneStatus Fail(Session session, ControlPlaneErrorCategory error)
        {
            if (session != _session)
            {
                return Superseded;
            }
            session.SetStatus(session.Source, error.Code());
            return session.Status;
        }

        private ControlPlaneStatus Activate(Session session, ControlPlaneSnapshot snapshot, ControlPlaneSource source, string lastError)
        {
            if (session != _session)
            {
                return Superseded;
            }
            if (snapshot != session.Active)
            {
                var resources = new Dictionary<string, JObject>(StringComparer.Ordinal);
                foreach (var resource in (JArray)snapshot.Root["resources"])
                {
                    resources.Add((string)resource["key"], (JObject)resource);
                }
                session.Active = snapshot;
                session.Resources = resources;
            }
            session.SetStatus(source, lastError);

            var status = session.Status;
            if (status.DeploymentId != session.AnnouncedDeploymentId)
            {
                session.AnnouncedDeploymentId = status.DeploymentId;
                Config.ResetDiagnostics();
                try
                {
                    DeploymentActivated?.Invoke(new ControlPlaneActivation(status));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            return status;
        }

        private sealed class Context : IEquatable<Context>
        {
            public Context(ControlPlaneOptions options)
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }
                if (!ControlPlaneDocuments.IsGameKey(options.GameKey))
                {
                    throw new ArgumentException("GameKey is malformed", nameof(options));
                }
                if (!ControlPlaneDocuments.IsEnvironmentKey(options.EnvironmentKey))
                {
                    throw new ArgumentException("EnvironmentKey is malformed", nameof(options));
                }
                if (!Uri.TryCreate(options.RuntimeOrigin, UriKind.Absolute, out var origin)
                    || origin.Scheme != Uri.UriSchemeHttps
                    || origin.Host.Length == 0
                    || origin.UserInfo.Length != 0
                    || origin.AbsolutePath != "/"
                    || origin.Query.Length != 0
                    || origin.Fragment.Length != 0)
                {
                    throw new ArgumentException("RuntimeOrigin is not an HTTPS origin", nameof(options));
                }
                if (options.RefreshIntervalSeconds != 0 && (options.RefreshIntervalSeconds < 15 || options.RefreshIntervalSeconds > 3600))
                {
                    throw new ArgumentException("RefreshIntervalSeconds is not 0 or 15..3600", nameof(options));
                }

                GameKey = options.GameKey;
                EnvironmentKey = options.EnvironmentKey;
                RuntimeOrigin = origin.GetLeftPart(UriPartial.Authority);
                RefreshIntervalSeconds = options.RefreshIntervalSeconds;
                BootstrapUrl = RuntimeOrigin + "/environments/" + GameKey + "/" + EnvironmentKey + "/bootstrap-v1.json";
            }

            public string GameKey { get; }
            public string EnvironmentKey { get; }
            public string RuntimeOrigin { get; }
            public int RefreshIntervalSeconds { get; }
            public string BootstrapUrl { get; }

            public bool Equals(Context other)
            {
                return other != null
                    && GameKey == other.GameKey
                    && EnvironmentKey == other.EnvironmentKey
                    && RuntimeOrigin == other.RuntimeOrigin
                    && RefreshIntervalSeconds == other.RefreshIntervalSeconds;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Context);
            }

            public override int GetHashCode()
            {
                return BootstrapUrl.GetHashCode() ^ RefreshIntervalSeconds;
            }
        }

        private sealed class Session
        {
            private volatile ControlPlaneActiveState _current;

            public Session(Context context, ControlPlaneLkgJournal journal, ControlPlaneFetcher fetcher)
            {
                Context = context;
                Journal = journal;
                Fetcher = fetcher;
                SetStatus(ControlPlaneSource.None, null);
            }

            public Context Context { get; }
            public ControlPlaneLkgJournal Journal { get; }
            public ControlPlaneFetcher Fetcher { get; }
            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
            public Task<ControlPlaneStatus> Refreshing { get; set; }
            public ControlPlaneSnapshot Active { get; set; }
            public Dictionary<string, JObject> Resources { get; set; }
            public ControlPlaneActiveState Current => _current;
            public bool Started { get; set; }
            public bool Persisted { get; set; }
            public string AnnouncedDeploymentId { get; set; }
            public ControlPlaneSource Source => Status.Source;
            public ControlPlaneStatus Status => _current.Status;

            public void SetStatus(ControlPlaneSource source, string lastError)
            {
                _current = new ControlPlaneActiveState(
                    new ControlPlaneStatus(source, Context.GameKey, Context.EnvironmentKey, Active?.Bootstrap.DeploymentId, Active?.Bootstrap.ReleaseId, lastError),
                    Resources);
            }
        }
    }
}
