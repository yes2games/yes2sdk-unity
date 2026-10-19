using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Yes2SDK;

namespace Yes2Games.CI
{
    /// <summary>Initializes the control plane against recorded cp-sandbox bytes and logs one <see cref="Marker"/> result line.</summary>
    public sealed class ControlPlaneNativeProof : MonoBehaviour
    {
        public const string Marker = "YES2SDK_CONTROL_PLANE_PROOF";

        private const string GameKey = "cp-sandbox";
        private const string EnvironmentKey = "development";
        private const string DeploymentId = "01f38c8c-6ad9-4e63-ab94-c959de80068d";
        private const string SnapshotDigest = "sha256:e8f79edc6a1172e5f9d988258a037f4225a104bfc0eaa233b970d9a483994530";
        private const string BootstrapUrl = Yes2SDKControlPlane.DefaultRuntimeOrigin + "/environments/" + GameKey + "/" + EnvironmentKey + "/bootstrap-v1.json";
        private const string SnapshotUrl = Yes2SDKControlPlane.DefaultRuntimeOrigin + "/releases/cp-sandbox/f34fe47e-4d94-4e53-bf19-7587a7c96977/runtime-snapshot-v1.json";
        private const string Bootstrap = @"{""deploymentId"":""01f38c8c-6ad9-4e63-ab94-c959de80068d"",""environmentKey"":""development"",""gameKey"":""cp-sandbox"",""protocolVersion"":1,""releaseId"":""f34fe47e-4d94-4e53-bf19-7587a7c96977"",""snapshotDigest"":""sha256:e8f79edc6a1172e5f9d988258a037f4225a104bfc0eaa233b970d9a483994530"",""snapshotUrl"":""https://runtime.yes2games.com/releases/cp-sandbox/f34fe47e-4d94-4e53-bf19-7587a7c96977/runtime-snapshot-v1.json""}";
        private const string Snapshot = @"{""artifacts"":[],""gameKey"":""cp-sandbox"",""protocolVersion"":1,""releaseId"":""f34fe47e-4d94-4e53-bf19-7587a7c96977"",""resources"":[{""data"":{""greeting"":""hello"",""run"":1},""key"":""e2e.config"",""revisionId"":""4c26a820-4815-4904-81f1-3434040dd5a3"",""typeKey"":""config.v1""}]}";

        private void Start()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            _ = RunAsync();
        }

        private static async Task RunAsync()
        {
            var mode = Application.absoluteURL.Contains("proof=reload") ? "reload" : "full";
            string reason = null;
            try
            {
                if (mode == "full")
                {
                    await WritesAndReloadsLkg();
                }
                else
                {
                    await ActivatesFromLkg();
                }
            }
            catch (Exception e)
            {
                reason = e.GetType().Name + ": " + e.Message;
            }

            var result = new JObject
            {
                ["result"] = reason == null ? "pass" : "fail",
                ["mode"] = mode,
                ["platform"] = Application.platform.ToString(),
                ["unity"] = Application.unityVersion,
                ["reason"] = reason
            };
            Debug.Log(Marker + " " + result.ToString(Formatting.None));
            if (Application.platform == RuntimePlatform.Android)
            {
                Application.Quit(reason == null ? 0 : 1);
            }
        }

        private static async Task WritesAndReloadsLkg()
        {
            var snapshot = Encoding.UTF8.GetBytes(Snapshot);
            Expect(ControlPlaneSha256.Digest(snapshot) == SnapshotDigest, "SHA-256 of the recorded snapshot is " + ControlPlaneSha256.Digest(snapshot));

            var journal = Path.Combine(Application.persistentDataPath, "Yes2SDK", "control-plane", "v1", GameKey, EnvironmentKey);
            if (Directory.Exists(journal))
            {
                Directory.Delete(journal, true);
            }

            var tampered = await Run.Initialize(Encoding.UTF8.GetBytes(Snapshot.Replace("\"hello\"", "\"hellp\"")));
            Expect(tampered.Status.Source == ControlPlaneSource.None && tampered.Status.LastError == "snapshot_invalid", "a snapshot that fails its digest gave " + Describe(tampered.Status));
            Expect(tampered.Activations.Count == 0, "a snapshot that fails its digest raised DeploymentActivated");
            Expect(!File.Exists(Path.Combine(journal, ControlPlaneLkgJournal.Pointer)), "a snapshot that fails its digest reached the LKG journal");

            var remote = await Run.Initialize(snapshot);
            Expect(remote.Status.Source == ControlPlaneSource.Remote && remote.Status.DeploymentId == DeploymentId && remote.Status.LastError == null, "remote initialization gave " + Describe(remote.Status));
            remote.ExpectOneActivation(ControlPlaneSource.Remote);
            remote.ExpectGreeting();
            Expect(File.Exists(Path.Combine(journal, ControlPlaneLkgJournal.Pointer)), "remote activation wrote no LKG pointer under " + journal);

            await ActivatesFromLkg();
        }

        private static async Task ActivatesFromLkg()
        {
            var offline = await Run.Initialize(null);
            Expect(offline.Status.Source == ControlPlaneSource.Lkg && offline.Status.DeploymentId == DeploymentId && offline.Status.LastError == "network_unavailable", "offline initialization gave " + Describe(offline.Status));
            offline.ExpectOneActivation(ControlPlaneSource.Lkg);
            offline.ExpectGreeting();
        }

        private static string Describe(ControlPlaneStatus status)
        {
            return "source=" + status.Source + " deploymentId=" + (status.DeploymentId ?? "-") + " lastError=" + (status.LastError ?? "-");
        }

        private static void Expect(bool condition, string failure)
        {
            if (!condition)
            {
                throw new Exception(failure);
            }
        }

        private sealed class Run
        {
            private readonly Yes2SDKControlPlane _controlPlane;

            private Run(Yes2SDKControlPlane controlPlane)
            {
                _controlPlane = controlPlane;
            }

            public ControlPlaneStatus Status { get; private set; }
            public List<ControlPlaneActivation> Activations { get; } = new List<ControlPlaneActivation>();
            private string Greeting { get; set; }

            public static async Task<Run> Initialize(byte[] snapshot)
            {
                var run = new Run(new Yes2SDKControlPlane(
                    new RecordedTransport(snapshot),
                    UnityWebRequestControlPlaneTransport.DelayAsync,
                    new System.Random(),
                    Yes2ControlPlane.OpenJournal));
                run._controlPlane.DeploymentActivated += run.Activations.Add;
                try
                {
                    run.Status = await run._controlPlane.InitializeAsync(
                        new ControlPlaneOptions { GameKey = GameKey, EnvironmentKey = EnvironmentKey, RefreshIntervalSeconds = 0 },
                        CancellationToken.None);
                    run.Greeting = run._controlPlane.Config.GetString("e2e.config", "/greeting", null);
                }
                finally
                {
                    run._controlPlane.Shutdown();
                }
                return run;
            }

            public void ExpectOneActivation(ControlPlaneSource source)
            {
                Expect(Activations.Count == 1 && Activations[0].DeploymentId == DeploymentId && Activations[0].Source == source,
                    "DeploymentActivated fired " + Activations.Count + " time(s); expected once from " + source);
            }

            public void ExpectGreeting()
            {
                Expect(Greeting == "hello", "Config.GetString(e2e.config, /greeting) returned " + (Greeting ?? "null"));
            }
        }

        private sealed class RecordedTransport : IControlPlaneTransport
        {
            private readonly byte[] _snapshot;

            public RecordedTransport(byte[] snapshot)
            {
                _snapshot = snapshot;
            }

            public Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
            {
                if (_snapshot == null)
                {
                    return Task.FromResult(ControlPlaneTransportResponse.NetworkUnavailable("recorded offline"));
                }
                switch (request.Url)
                {
                    case BootstrapUrl:
                        return Task.FromResult(ControlPlaneTransportResponse.Completed(200, Encoding.UTF8.GetBytes(Bootstrap)));
                    case SnapshotUrl:
                        return Task.FromResult(ControlPlaneTransportResponse.Completed(200, _snapshot));
                    default:
                        return Task.FromResult(ControlPlaneTransportResponse.Completed(404, null));
                }
            }
        }
    }
}
