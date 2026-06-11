using UnityEngine;
using UnityEditor;
using System.IO;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif

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
        private const string PrefsSettingsExpanded = "Yes2SDK_SettingsExpanded";
        private const string DashboardUrl = "https://dashboard.yes2games.com";
        private const string DocsUrl = "https://github.com/yes2games/yes2sdk-unity";
        private const string IssuesUrl = "https://github.com/yes2games/yes2sdk-unity/issues";

        private string _buildPath;
        private bool _isSetupComplete;
        private bool _settingsExpanded;

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
            _settingsExpanded = EditorPrefs.GetBool(PrefsSettingsExpanded, false);
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

            DrawPipelineToggle();
            EditorGUILayout.Space(10);

            if (!_isSetupComplete)
            {
                DrawSetup();
            }
            else
            {
                DrawSettings();
                EditorGUILayout.Space(10);

                DrawBuildMode();
                EditorGUILayout.Space(10);

                DrawBuild();
            }

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

        private void DrawPipelineToggle()
        {
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.ToggleLeft(
                new GUIContent("Use Yes2SDK build pipeline (enforce WebGL template & build mode)",
                    "On: Yes2SDK enforces its WebGL template on every build (recommended for Yes2Games builds). " +
                    "Off: the template guard and build-mode override are skipped so another platform's pipeline " +
                    "(with its own WebGL template) can build without being blocked. The SDK stays installed either way."),
                Yes2SDKPipeline.Enabled);
            if (EditorGUI.EndChangeCheck())
                Yes2SDKPipeline.Enabled = enabled;

            if (!Yes2SDKPipeline.Enabled)
            {
                EditorGUILayout.HelpBox(
                    "Yes2SDK build management is OFF. The template guard is skipped — " +
                    "use this when building for a non-Yes2SDK platform. Turn it back on " +
                    "before building for Yes2Games platforms.",
                    MessageType.Warning);
            }
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

        private void DrawSettings()
        {
            // Collapsible — defaults closed so the Build Window stays compact
            // for users who don't need to fiddle. Persists open/closed state
            // across Editor sessions.
            EditorGUI.BeginChangeCheck();
            _settingsExpanded = EditorGUILayout.Foldout(
                _settingsExpanded,
                "WebGL Settings",
                toggleOnLabelClick: true,
                EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(PrefsSettingsExpanded, _settingsExpanded);

            if (!_settingsExpanded) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Edits apply to project-wide Player Settings immediately — no Apply step.",
                EditorStyles.miniLabel);

#if UNITY_6000_0_OR_NEWER
            // Unity 6 Build Profiles can override Player Settings on a
            // per-profile basis. If one is active, edits made here might be
            // shadowed at build time — warn the user explicitly so this isn't
            // a silent footgun.
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null)
            {
                EditorGUILayout.HelpBox(
                    $"Active Build Profile: \"{activeProfile.name}\".\n" +
                    "If this profile has overrides for the WebGL settings below, " +
                    "they will take precedence at build time. To edit the " +
                    "profile's overrides instead, use File > Build Profiles.",
                    MessageType.Info);
            }
#endif
            EditorGUILayout.Space(4);

            // Template — informational only. BuildGuard enforces this; show a
            // warning if it drifts.
            string currentTemplate = PlayerSettings.WebGL.template;
            string expected = "PROJECT:Yes2SDK-SuperSDK";
            EditorGUILayout.LabelField(
                new GUIContent("Template",
                    "Yes2SDK-SuperSDK is required. Other templates won't have the JS bridge wired up — Yes2SDKBuildGuard will fail the build."),
                new GUIContent(currentTemplate));
            if (currentTemplate != expected)
            {
                EditorGUILayout.HelpBox(
                    $"Template should be {expected}. Click \"Reset to recommended\" or set it via the Build Profile's Player Settings.",
                    MessageType.Warning);
            }

            // Compression Format
            EditorGUI.BeginChangeCheck();
            var newCompression = (WebGLCompressionFormat)EditorGUILayout.EnumPopup(
                new GUIContent("Compression",
                    "Disabled is required for Yes2Games dashboard upload — the dashboard CDN doesn't currently send Content-Encoding headers, so Brotli/Gzip builds fail to decompress in browser."),
                PlayerSettings.WebGL.compressionFormat);
            if (EditorGUI.EndChangeCheck())
                PlayerSettings.WebGL.compressionFormat = newCompression;

            // Code Stripping
            EditorGUI.BeginChangeCheck();
            var currentStripping = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL);
            var newStripping = (ManagedStrippingLevel)EditorGUILayout.EnumPopup(
                new GUIContent("Code Stripping",
                    "Medium balances build size and AOT safety — recommended. High requires manual link.xml entries for reflection-only types. Low produces large builds; useful for debugging strip-related issues."),
                currentStripping);
            if (EditorGUI.EndChangeCheck())
                PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, newStripping);

            // Exception Support
            EditorGUI.BeginChangeCheck();
            var newException = (WebGLExceptionSupport)EditorGUILayout.EnumPopup(
                new GUIContent("Exception Support",
                    "Explicitly Thrown is recommended for most games — catches throws from Newtonsoft.Json and other libraries. None is smaller but breaks any try/catch path. Full With Stacktrace is largest; use only for diagnostics."),
                PlayerSettings.WebGL.exceptionSupport);
            if (EditorGUI.EndChangeCheck())
                PlayerSettings.WebGL.exceptionSupport = newException;

            // Initial Memory Size
            EditorGUI.BeginChangeCheck();
            int newMemory = EditorGUILayout.IntField(
                new GUIContent("Memory Size (MB)",
                    "Initial WebAssembly heap size. Most games need 256–512+ MB. Too small triggers a generic 'unspecified error' at boot when Unity can't allocate the heap."),
                PlayerSettings.WebGL.initialMemorySize);
            if (EditorGUI.EndChangeCheck())
                // Floor at 32 MB — Unity's empty-project default and a
                // practical minimum for any real game. Lower values often
                // trigger "unspecified error" at boot before Unity can even
                // log a useful failure.
                PlayerSettings.WebGL.initialMemorySize = Mathf.Max(32, newMemory);

            EditorGUILayout.Space(6);

            // Reset button — only opt-in path back to BuildConfig.Default
            // values. Was previously the auto-applied step on every build,
            // which caused issue #40.
            if (GUILayout.Button(
                new GUIContent("Reset to recommended",
                    "Reset all WebGL settings above to Yes2SDK's recommended values (Yes2SDK-SuperSDK template, Disabled compression, Medium stripping, Explicitly Thrown exceptions)."),
                EditorStyles.miniButton))
            {
                ResetToRecommended();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildMode()
        {
            EditorGUILayout.LabelField("Build Mode", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Dropdown — only one mode is active at a time, so a popup is
            // more honest than a radio group and stays compact for users
            // who never need to change it.
            var current = Yes2SDKBuildMode.Current;
            // Build option labels from the enum + DisplayName helper so they
            // never drift if the helper text changes.
            var modes = (Yes2SDKBuildMode.Mode[])System.Enum.GetValues(typeof(Yes2SDKBuildMode.Mode));
            var modeLabels = new string[modes.Length];
            for (int i = 0; i < modes.Length; i++)
                modeLabels[i] = Yes2SDKBuildMode.DisplayName(modes[i]);

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Mode", (int)current, modeLabels);
            if (EditorGUI.EndChangeCheck())
            {
                Yes2SDKBuildMode.Current = (Yes2SDKBuildMode.Mode)newIndex;
            }

            EditorGUILayout.Space(4);

            // Detail block for the currently-selected mode — visible by
            // default (no hover required), updates live as the user picks
            // different modes from the dropdown.
            string description = Yes2SDKBuildMode.Current switch
            {
                Yes2SDKBuildMode.Mode.Production =>
                    "Use your Player Settings as-is. Pick this for shipping builds when " +
                    "Exception Support is already set the way you want it (recommended " +
                    "default: Explicitly Thrown — click \"Reset to recommended\" in " +
                    "WebGL Settings above).",
                Yes2SDKBuildMode.Mode.ProductionSafe =>
                    "Force Exception Support → Explicitly Thrown for this build, restore " +
                    "Player Settings after. Pick this when your Player Settings has " +
                    "Exception Support: None but you still need to ship a build that " +
                    "catches third-party throws (Newtonsoft.Json, i2 Localization, etc.). " +
                    "About 10% larger than None — fine to ship.",
                Yes2SDKBuildMode.Mode.Diagnostic =>
                    "Force Exception Support → Full With Stacktrace for this build, " +
                    "restore after. Pick this only while debugging — captures real C# " +
                    "class/method names and line numbers in browser console errors. " +
                    "About 30% larger — DO NOT ship Diagnostic builds to the dashboard.",
                _ => string.Empty,
            };

            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

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

            // Clean Build — wipes the WebGL output folder before building so leftover
            // files from a prior failed build can't pollute the new one. Useful when:
            // - switching Build Modes (Production / Production Safe / Diagnostic)
            // - the previous build failed mid-way and left partial output
            // - YouTube/cert testing wants a known-clean upload artifact
            if (GUILayout.Button("Clean Build", GUILayout.Height(22)))
                CleanBuild();

            EditorGUILayout.Space(6);

            // Reinstall template — only tertiary action remaining; "Apply
            // Settings" moved into the WebGL Settings foldout as
            // "Reset to recommended".
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reinstall Template", EditorStyles.linkLabel))
                PerformSetup();
            GUILayout.Label("·", EditorStyles.miniLabel);
            if (GUILayout.Button("Clear Build Cache", EditorStyles.linkLabel))
                ClearBuildCache();
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

        private void ResetToRecommended()
        {
            BuildConfig.Default.ApplySettings();
            Debug.Log("[Yes2SDK] WebGL settings reset to recommended values (template, compression, stripping, exception support).");
        }

        /// <summary>
        /// Wipe the WebGL output folder, then run a fresh build. Used to guarantee
        /// no leftover files from a prior build pollute the new one — particularly
        /// important before YouTube Playables / cert submissions where the upload
        /// must be a clean artifact.
        /// </summary>
        private void CleanBuild()
        {
            if (!Yes2SDKInstaller.IsSetupComplete())
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "WebGL template not installed.\n\nClick Install Template first.", "OK");
                return;
            }

            string fullBuildPath = Path.Combine(_buildPath, "WebGL");
            bool exists = Directory.Exists(fullBuildPath);

            string message = exists
                ? $"Delete the existing WebGL build at:\n\n{fullBuildPath}\n\nThen run a fresh build?"
                : "No existing WebGL build found.\n\nProceed with a fresh build?";

            bool confirmed = EditorUtility.DisplayDialog(
                "Clean Build",
                message,
                exists ? "Delete and Build" : "Build",
                "Cancel");

            if (!confirmed) return;

            if (exists)
            {
                try
                {
                    Directory.Delete(fullBuildPath, recursive: true);
                    Debug.Log($"[Yes2SDK] Clean Build: deleted {fullBuildPath}");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Yes2SDK",
                        $"Could not delete the existing build folder:\n\n{e.Message}\n\nClose any open Explorer / Finder windows pointing at it and try again.", "OK");
                    return;
                }
            }

            BuildGame(false);
        }

        /// <summary>
        /// Clear Unity's WebGL incremental build cache (Library/Bee/artifacts/WebGL
        /// and Library/PlayerDataCache). Used as a last resort when builds produce
        /// stale or corrupted output even after a Clean Build. Forces Unity to
        /// recompile shaders, re-strip assemblies, and re-link emscripten output
        /// — adds 2-5 minutes to the next build but resolves "build appears to be
        /// corrupted" / mismatched assembly errors that don't go away otherwise.
        ///
        /// Does NOT delete Library/ScriptAssemblies or Library/PackageCache, so
        /// the project does not need a full reimport.
        /// </summary>
        private void ClearBuildCache()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Build Cache",
                "Clear Unity's WebGL incremental build cache?\n\n" +
                "This forces the next build to recompile shaders and re-link " +
                "WebGL output (slower by 2-5 minutes), but resolves rare " +
                "build-corruption issues.\n\n" +
                "Your scenes, scripts, and packages are not affected.",
                "Clear Cache",
                "Cancel");

            if (!confirmed) return;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] cachePaths = new[]
            {
                Path.Combine(projectRoot, "Library", "Bee", "artifacts", "WebGL"),
                Path.Combine(projectRoot, "Library", "PlayerDataCache"),
                Path.Combine(projectRoot, "Library", "il2cpp_cache"),
            };

            int deleted = 0;
            foreach (var path in cachePaths)
            {
                if (!Directory.Exists(path)) continue;
                try
                {
                    Directory.Delete(path, recursive: true);
                    Debug.Log($"[Yes2SDK] Clear Build Cache: deleted {path}");
                    deleted++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Yes2SDK] Clear Build Cache: could not delete {path} — {e.Message}");
                }
            }

            EditorUtility.DisplayDialog(
                "Clear Build Cache",
                deleted > 0
                    ? $"Cleared {deleted} cache folder{(deleted == 1 ? "" : "s")}. The next build will be slower but should produce fresh output."
                    : "No cache folders found to clear. The build cache may already be empty.",
                "OK");
        }

        private void BuildGame(bool runAfterBuild)
        {
            if (!Yes2SDKInstaller.IsSetupComplete())
            {
                EditorUtility.DisplayDialog("Yes2SDK",
                    "WebGL template not installed.\n\nClick Install Template first.", "OK");
                return;
            }

            // No auto-apply of BuildConfig.Default here. Whatever the user has
            // configured in Player Settings (or chosen via the Build Mode
            // radio's IPreprocessBuildWithReport override) is what the build
            // uses. Yes2SDKBuildGuard still enforces the template requirement.

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
