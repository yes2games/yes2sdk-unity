using UnityEngine;
using UnityEditor;
using System.IO;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Yes2SDK Editor Window - Build tools and utilities for WebGL games.
    /// </summary>
    public class Yes2SDKWindow : EditorWindow
    {
        private TargetPlatform _selectedPlatform = TargetPlatform.Debug;
        private string _buildPath = "Builds";
        private bool _showBuildSettings = true;
        private bool _showUtilities = true;
        private Vector2 _scrollPosition;
        private bool _isSetupComplete;

        [MenuItem("Yes2SDK/Settings", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<Yes2SDKWindow>("Yes2SDK");
            window.minSize = new Vector2(350, 400);
            window.Show();
        }

        [MenuItem("Yes2SDK/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/yes2games/yes2sdk-unity");
        }

        private void OnEnable()
        {
            // Load saved preferences
            _selectedPlatform = (TargetPlatform)EditorPrefs.GetInt("Yes2SDK_SelectedPlatform", (int)TargetPlatform.Debug);
            _buildPath = EditorPrefs.GetString("Yes2SDK_BuildPath", "Builds");

            // Check setup status
            RefreshSetupStatus();
        }

        private void OnFocus()
        {
            // Refresh setup status when window gains focus
            RefreshSetupStatus();
        }

        private void RefreshSetupStatus()
        {
            _isSetupComplete = Yes2SDKInstaller.IsSetupComplete();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            // Show setup section if not complete
            if (!_isSetupComplete)
            {
                DrawSetupSection();
                EditorGUILayout.Space(10);
            }

            DrawBuildSection();
            EditorGUILayout.Space(10);

            DrawUtilitiesSection();
            EditorGUILayout.Space(10);

            DrawFooter();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Yes2SDK", headerStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("WebGL Build Tools", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Warning icon style
            var warningStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(1f, 0.7f, 0.2f) },
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Setup Required", warningStyle);

            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Yes2SDK needs to be set up before you can build.\n\n" +
                "This will:\n" +
                "• Install WebGL templates (Poki, CrazyGames, Debug)\n" +
                "• Configure project settings for WebGL games",
                MessageType.Warning);

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Setup Yes2SDK", GUILayout.Height(35)))
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
                    "Setup completed successfully!\n\n" +
                    "Templates installed:\n" +
                    "• Yes2SDK (Debug)\n" +
                    "• Yes2SDK-Poki\n" +
                    "• Yes2SDK-CrazyGames\n\n" +
                    "You can now build for any platform.",
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
            _showBuildSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showBuildSettings, "Build Settings");

            if (_showBuildSettings)
            {
                // Disable build section if setup not complete
                EditorGUI.BeginDisabledGroup(!_isSetupComplete);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Platform selection
                EditorGUI.BeginChangeCheck();
                _selectedPlatform = (TargetPlatform)EditorGUILayout.EnumPopup("Target Platform", _selectedPlatform);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt("Yes2SDK_SelectedPlatform", (int)_selectedPlatform);
                }

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
                        // Make relative if inside project
                        string projectPath = Directory.GetParent(Application.dataPath).FullName;
                        if (path.StartsWith(Application.dataPath))
                        {
                            path = "Assets" + path.Substring(Application.dataPath.Length);
                        }
                        else if (path.StartsWith(projectPath))
                        {
                            path = path.Substring(projectPath.Length + 1);
                        }
                        _buildPath = path;
                        EditorPrefs.SetString("Yes2SDK_BuildPath", _buildPath);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Platform info box
                var config = BuildConfig.GetConfig(_selectedPlatform);
                DrawPlatformInfo(config);

                EditorGUILayout.Space(10);

                // Build buttons
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Apply Settings", GUILayout.Height(30)))
                {
                    ApplyBuildSettings();
                }

                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("Build", GUILayout.Height(30)))
                {
                    BuildForPlatform();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                // Build & Run button
                if (GUILayout.Button("Build and Run", GUILayout.Height(25)))
                {
                    BuildForPlatform(runAfterBuild: true);
                }

                EditorGUILayout.Space(5);

                // Reinstall templates link
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reinstall Templates", EditorStyles.linkLabel))
                {
                    ReinstallTemplates();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUI.EndDisabledGroup();

                // Show hint if disabled
                if (!_isSetupComplete)
                {
                    EditorGUILayout.HelpBox("Complete setup above to enable build options.", MessageType.Info);
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPlatformInfo(BuildConfig config)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var labelStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            EditorGUILayout.LabelField(config.Description, labelStyle);

            EditorGUILayout.Space(5);

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Template", config.TemplateName);
            EditorGUILayout.LabelField("Compression", config.Compression.ToString());
            EditorGUILayout.LabelField("Code Stripping", config.CodeStripping.ToString());
            EditorGUILayout.LabelField("Exception Support", config.ExceptionSupport.ToString());
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        private void DrawUtilitiesSection()
        {
            _showUtilities = EditorGUILayout.BeginFoldoutHeaderGroup(_showUtilities, "Utilities");

            if (_showUtilities)
            {
                EditorGUI.BeginDisabledGroup(!_isSetupComplete);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Coming Soon", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(5);

                GUI.enabled = false;
                GUILayout.Button("Letterbox (Portrait Mode)");
                GUILayout.Button("Safe Area Handler");
                GUILayout.Button("Build Size Analyzer");
                GUI.enabled = true;

                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Utility tools will be added in future updates.", MessageType.Info);

                EditorGUILayout.EndVertical();

                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Documentation", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://github.com/yes2games/yes2sdk-unity");
            }

            GUILayout.Label("|", EditorStyles.miniLabel);

            if (GUILayout.Button("Report Issue", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://github.com/yes2games/yes2sdk-unity/issues");
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Status indicator
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
            GUILayout.Label("v1.0.0", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ReinstallTemplates()
        {
            Debug.Log("[Yes2SDK] Reinstalling templates...");

            bool success = Yes2SDKInstaller.InstallAllTemplates();

            if (success)
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "Templates reinstalled successfully!",
                    "OK");
                RefreshSetupStatus();
            }
            else
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "Failed to reinstall templates.\n\nCheck the Console for details.",
                    "OK");
            }
        }

        private void ApplyBuildSettings()
        {
            var config = BuildConfig.GetConfig(_selectedPlatform);
            config.ApplySettings();

            EditorUtility.DisplayDialog("Yes2SDK",
                $"Build settings applied for {config.DisplayName}.\n\n" +
                $"Template: {config.TemplateName}\n" +
                $"Compression: {config.Compression}",
                "OK");
        }

        private void BuildForPlatform(bool runAfterBuild = false)
        {
            var config = BuildConfig.GetConfig(_selectedPlatform);

            // Verify template exists in project's Assets folder
            string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates", config.TemplateName);
            Debug.Log($"[Yes2SDK] Checking for template at: {templatePath}");

            if (!Directory.Exists(templatePath))
            {
                Debug.LogError($"[Yes2SDK] Template not found at: {templatePath}");
                Debug.LogError("[Yes2SDK] Templates must be installed to your project's Assets/WebGLTemplates/ folder.");
                Debug.LogError("[Yes2SDK] Click 'Setup Yes2SDK' button to install templates.");

                EditorUtility.DisplayDialog("Yes2SDK",
                    $"Template '{config.TemplateName}' not found.\n\n" +
                    $"Expected at:\n{templatePath}\n\n" +
                    "Click 'Setup Yes2SDK' to install templates to your project.",
                    "OK");
                return;
            }

            Debug.Log($"[Yes2SDK] Template found: {config.TemplateName}");

            // Apply settings first
            config.ApplySettings();

            // Get scenes to build
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "No scenes in build settings.\nPlease add scenes via File > Build Settings.",
                    "OK");
                return;
            }

            var scenePaths = new string[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                scenePaths[i] = scenes[i].path;
            }

            // Build path
            string fullBuildPath = Path.Combine(_buildPath, config.Platform.ToString());
            string buildName = PlayerSettings.productName;

            // Ensure directory exists
            if (!Directory.Exists(fullBuildPath))
            {
                Directory.CreateDirectory(fullBuildPath);
            }

            Debug.Log($"[Yes2SDK] Building for {config.DisplayName} to {fullBuildPath}");

            // Build options
            var buildOptions = BuildOptions.None;
            if (runAfterBuild)
            {
                buildOptions |= BuildOptions.AutoRunPlayer;
            }

            // Start build
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = Path.Combine(fullBuildPath, buildName),
                target = BuildTarget.WebGL,
                options = buildOptions
            });

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[Yes2SDK] Build succeeded: {fullBuildPath}");
                EditorUtility.DisplayDialog("Yes2SDK",
                    $"Build completed successfully!\n\nOutput: {fullBuildPath}",
                    "OK");

                // Open folder
                EditorUtility.RevealInFinder(fullBuildPath);
            }
            else
            {
                Debug.LogError($"[Yes2SDK] Build failed with {report.summary.totalErrors} errors");
                EditorUtility.DisplayDialog("Yes2SDK",
                    $"Build failed with {report.summary.totalErrors} errors.\nCheck the Console for details.",
                    "OK");
            }
        }
    }
}
