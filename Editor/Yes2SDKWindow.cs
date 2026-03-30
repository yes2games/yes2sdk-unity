using UnityEngine;
using UnityEditor;
using System.IO;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Yes2SDK Editor Window — simplified for SuperSDK pipeline.
    /// Build WebGL → Upload to Dashboard → Dashboard handles platform bundling.
    /// </summary>
    public class Yes2SDKWindow : EditorWindow
    {
        private string _buildPath = "Builds";
        private bool _showDebugLogs = true;
        private Vector2 _scrollPosition;
        private bool _isSetupComplete;

        [MenuItem("Yes2SDK/Build Window", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<Yes2SDKWindow>("Yes2SDK");
            window.minSize = new Vector2(350, 300);
            window.Show();
        }

        [MenuItem("Yes2SDK/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/yes2games/yes2sdk-unity");
        }

        private void OnEnable()
        {
            _buildPath = EditorPrefs.GetString("Yes2SDK_BuildPath", "Builds");
            _showDebugLogs = EditorPrefs.GetBool("Yes2SDK_ShowDebugLogs", true);
            RefreshSetupStatus();
        }

        private void OnFocus()
        {
            RefreshSetupStatus();
        }

        private void RefreshSetupStatus()
        {
            // Check if the SuperSDK template exists in the project
            string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates", "Yes2SDK-SuperSDK");
            _isSetupComplete = Directory.Exists(templatePath);
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(10);

            if (!_isSetupComplete)
            {
                DrawSetupSection();
                EditorGUILayout.Space(10);
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawBuildSection();
            EditorGUILayout.Space(10);
            DrawSettingsSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(5);
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("Yes2SDK", headerStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("SuperSDK Pipeline", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var warningStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(1f, 0.7f, 0.2f) },
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Setup Required", warningStyle);

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Yes2SDK needs to install the WebGL template before you can build.\n\n" +
                "This will install the Yes2SDK-SuperSDK template to your project.",
                MessageType.Warning);

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Install Template", GUILayout.Height(35)))
            {
                PerformSetup();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void PerformSetup()
        {
            bool success = Yes2SDKInstaller.PerformSetup();
            if (success)
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "Setup completed!\n\n" +
                    "Template installed: Yes2SDK-SuperSDK\n\n" +
                    "Build your game, then upload the zip to the Yes2SDK Dashboard.\n" +
                    "The dashboard handles platform-specific SDK injection.",
                    "OK");
                RefreshSetupStatus();
            }
            else
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "Setup failed.\n\nCheck the Console for details.",
                    "OK");
            }
        }

        private void DrawBuildSection()
        {
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!_isSetupComplete);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Build info
            var config = BuildConfig.Default;
            EditorGUILayout.HelpBox(config.Description, MessageType.Info);

            EditorGUILayout.Space(5);

            // Build path
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _buildPath = EditorGUILayout.TextField("Build Path", _buildPath);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString("Yes2SDK_BuildPath", _buildPath);
            }
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.SaveFolderPanel("Select Build Folder", _buildPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string projectPath = Directory.GetParent(Application.dataPath).FullName;
                    if (path.StartsWith(projectPath))
                        path = path.Substring(projectPath.Length + 1);
                    _buildPath = path;
                    EditorPrefs.SetString("Yes2SDK_BuildPath", _buildPath);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Config details
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Template", config.TemplateName);
            EditorGUILayout.LabelField("Compression", config.Compression.ToString());
            EditorGUILayout.LabelField("Code Stripping", config.CodeStripping.ToString());
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            // Build buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Apply Settings", GUILayout.Height(30)))
            {
                config.ApplySettings();
                EditorUtility.DisplayDialog("Yes2SDK",
                    $"Build settings applied.\n\nTemplate: {config.TemplateName}",
                    "OK");
            }

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Build WebGL", GUILayout.Height(30)))
            {
                BuildGame(false);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Build and Run", GUILayout.Height(25)))
            {
                BuildGame(true);
            }

            EditorGUILayout.Space(5);

            // Workflow hint
            EditorGUILayout.HelpBox(
                "After building:\n" +
                "1. Zip the build output folder\n" +
                "2. Upload to Yes2SDK Dashboard\n" +
                "3. Select target platforms\n" +
                "4. Download platform-specific bundles",
                MessageType.None);

            EditorGUILayout.EndVertical();
            EditorGUI.EndDisabledGroup();

            if (!_isSetupComplete)
            {
                EditorGUILayout.HelpBox("Complete setup above to enable build.", MessageType.Info);
            }
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _showDebugLogs = EditorGUILayout.Toggle("Show Debug Logs", _showDebugLogs);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("Yes2SDK_ShowDebugLogs", _showDebugLogs);
            }

            EditorGUILayout.Space(5);

            // Reinstall template
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reinstall Template", EditorStyles.linkLabel))
            {
                PerformSetup();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_isSetupComplete)
            {
                var readyStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.green } };
                GUILayout.Label("● Ready", readyStyle);
            }
            else
            {
                var pendingStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } };
                GUILayout.Label("● Setup Pending", pendingStyle);
            }

            GUILayout.Label(" | ", EditorStyles.miniLabel);
            GUILayout.Label("v2.0.0-alpha", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void BuildGame(bool runAfterBuild)
        {
            var config = BuildConfig.Default;

            // Verify template
            string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates", config.TemplateName);
            if (!Directory.Exists(templatePath))
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    $"Template '{config.TemplateName}' not found.\n\nClick 'Install Template' first.",
                    "OK");
                return;
            }

            config.ApplySettings();

            // Get scenes
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "No scenes in build settings.\nAdd scenes via File > Build Settings.",
                    "OK");
                return;
            }

            var scenePaths = new string[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                scenePaths[i] = scenes[i].path;

            string fullBuildPath = Path.Combine(_buildPath, "WebGL");
            if (!Directory.Exists(fullBuildPath))
                Directory.CreateDirectory(fullBuildPath);

            Debug.Log($"[Yes2SDK] Building WebGL to {fullBuildPath}");

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
                Debug.Log($"[Yes2SDK] Build succeeded: {fullBuildPath}");
                EditorUtility.DisplayDialog("Yes2SDK",
                    $"Build completed!\n\nOutput: {fullBuildPath}\n\n" +
                    "Next: Zip this folder and upload to the Yes2SDK Dashboard.",
                    "OK");
                EditorUtility.RevealInFinder(fullBuildPath);
            }
            else
            {
                Debug.LogError($"[Yes2SDK] Build failed with {report.summary.totalErrors} errors");
                EditorUtility.DisplayDialog("Yes2SDK",
                    $"Build failed with {report.summary.totalErrors} errors.\nCheck Console.",
                    "OK");
            }
        }
    }
}
