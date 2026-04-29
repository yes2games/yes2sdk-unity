using UnityEngine;
using UnityEditor;
using System.IO;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Yes2SDK Editor Window — single-purpose control panel for the SuperSDK
    /// pipeline. Shows project readiness at a glance, builds for WebGL, and
    /// links out to the dashboard / docs / feedback. Everything else lives in
    /// the dashboard.
    /// </summary>
    public class Yes2SDKWindow : EditorWindow
    {
        private const string PrefsBuildPath = "Yes2SDK_BuildPath";
        private const string DashboardUrl = "https://dashboard.yes2games.com";
        private const string DocsUrl = "https://github.com/yes2games/yes2sdk-unity";
        private const string IssuesUrl = "https://github.com/yes2games/yes2sdk-unity/issues";

        private string _buildPath;
        private bool _isSetupComplete;

        [MenuItem("Yes2SDK/Build Window", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<Yes2SDKWindow>("Yes2SDK");
            window.minSize = new Vector2(340, 340);
            window.Show();
        }

        [MenuItem("Yes2SDK/Documentation", false, 100)]
        public static void OpenDocumentation() => Application.OpenURL(DocsUrl);

        [MenuItem("Yes2SDK/Dashboard", false, 101)]
        public static void OpenDashboard() => Application.OpenURL(DashboardUrl);

        private void OnEnable()
        {
            _buildPath = EditorPrefs.GetString(PrefsBuildPath, "Builds");
            RefreshSetupStatus();
        }

        private void OnFocus() => RefreshSetupStatus();

        private void RefreshSetupStatus()
        {
            _isSetupComplete = Yes2SDKInstaller.IsSetupComplete();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(12);

            DrawStatus();
            EditorGUILayout.Space(10);

            if (!_isSetupComplete) DrawSetup();
            else DrawBuild();

            GUILayout.FlexibleSpace();
            DrawLinks();
            DrawFooter();
        }

        private void DrawHeader()
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Yes2SDK", titleStyle);

            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"v{Yes2SDK.Version} · SuperSDK pipeline", subStyle);
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatusLine(
                _isSetupComplete,
                "Template installed",
                "Template not installed");

            DrawStatusLine(
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL,
                "WebGL build target active",
                "Switch build target to WebGL (File > Build Profiles)");

            int sceneCount = 0;
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) sceneCount++;
            DrawStatusLine(
                sceneCount > 0,
                $"{sceneCount} scene{(sceneCount == 1 ? "" : "s")} in Build Settings",
                "No scenes in Build Settings — add via File > Build Settings");

            EditorGUILayout.EndVertical();
        }

        private static void DrawStatusLine(bool ok, string okText, string warnText)
        {
            EditorGUILayout.BeginHorizontal();
            var dotStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = ok ? new Color(0.4f, 0.8f, 0.4f) : new Color(1f, 0.7f, 0.2f) },
                fixedWidth = 14
            };
            GUILayout.Label(ok ? "●" : "●", dotStyle);
            GUILayout.Label(ok ? okText : warnText, EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSetup()
        {
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Install the Yes2SDK-SuperSDK WebGL template before building.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Install Template", GUILayout.Height(32)))
            {
                PerformSetup();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawBuild()
        {
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Build path row.
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _buildPath = EditorGUILayout.TextField("Output", _buildPath);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PrefsBuildPath, _buildPath);

            if (GUILayout.Button("…", GUILayout.Width(28)))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string picked = EditorUtility.SaveFolderPanel("Build Folder", _buildPath, "");
                if (!string.IsNullOrEmpty(picked))
                {
                    if (picked.StartsWith(projectRoot))
                        picked = picked.Substring(projectRoot.Length + 1);
                    _buildPath = picked;
                    EditorPrefs.SetString(PrefsBuildPath, _buildPath);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Primary action.
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Build WebGL", GUILayout.Height(34)))
                BuildGame(false);
            GUI.backgroundColor = Color.white;

            // Secondary action.
            if (GUILayout.Button("Build and Run", GUILayout.Height(22)))
                BuildGame(true);

            EditorGUILayout.Space(6);

            // Inline tertiary actions.
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Settings", EditorStyles.linkLabel))
                ApplySettingsOnly();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reinstall Template", EditorStyles.linkLabel))
                PerformSetup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawLinks()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Dashboard", EditorStyles.linkLabel))
                Application.OpenURL(DashboardUrl);
            GUILayout.Label("·", EditorStyles.miniLabel);
            if (GUILayout.Button("Docs", EditorStyles.linkLabel))
                Application.OpenURL(DocsUrl);
            GUILayout.Label("·", EditorStyles.miniLabel);
            if (GUILayout.Button("Feedback", EditorStyles.linkLabel))
                Application.OpenURL(IssuesUrl);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var ready = _isSetupComplete;
            var statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = ready ? new Color(0.4f, 0.8f, 0.4f) : new Color(1f, 0.7f, 0.2f) }
            };
            GUILayout.Label(ready ? "● Ready" : "● Setup pending", statusStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void PerformSetup()
        {
            bool success = Yes2SDKInstaller.PerformSetup();
            if (success)
            {
                RefreshSetupStatus();
                Debug.Log("[Yes2SDK] Template installed.");
            }
            else
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "Setup failed — check the Console for details.", "OK");
            }
        }

        private void ApplySettingsOnly()
        {
            BuildConfig.Default.ApplySettings();
            Debug.Log("[Yes2SDK] Build settings applied (template, compression, stripping).");
        }

        private void BuildGame(bool runAfterBuild)
        {
            var config = BuildConfig.Default;

            if (!Yes2SDKInstaller.IsSetupComplete())
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "WebGL template not installed.\n\nClick Install Template first.", "OK");
                return;
            }

            config.ApplySettings();

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "No scenes in build settings.\nAdd scenes via File > Build Settings.", "OK");
                return;
            }

            var scenePaths = new string[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                scenePaths[i] = scenes[i].path;

            string fullBuildPath = Path.Combine(_buildPath, "WebGL");
            if (!Directory.Exists(fullBuildPath))
                Directory.CreateDirectory(fullBuildPath);

            Debug.Log($"[Yes2SDK] Building WebGL → {fullBuildPath}");

            var buildOptions = runAfterBuild ? BuildOptions.AutoRunPlayer : BuildOptions.None;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = Path.Combine(fullBuildPath, PlayerSettings.productName),
                target = BuildTarget.WebGL,
                options = buildOptions
            });

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[Yes2SDK] Build succeeded → {fullBuildPath}");
                EditorUtility.RevealInFinder(fullBuildPath);
            }
            else
            {
                Debug.LogError($"[Yes2SDK] Build failed ({report.summary.totalErrors} errors). Check Console.");
            }
        }
    }
}
