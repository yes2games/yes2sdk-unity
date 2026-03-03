using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Yes2SDK Editor Window - Build tools and utilities for WebGL games.
    /// </summary>
    public class Yes2SDKWindow : EditorWindow
    {
        private TargetPlatform _selectedPlatform = TargetPlatform.Debug;
        private string _buildPath = "Builds";
        private bool _showDebugLogs = true;
        private Vector2 _scrollPosition;
        private bool _isSetupComplete;

        // Tab state
        private int _selectedTab;
        private static readonly string[] _tabLabels = { "Build", "Optimization", "Tools" };

        // Sprite Atlas tool state
        private string _spriteAtlasSourceFolder = "Assets/Sprites";
        private string _spriteAtlasOutputDir = "Assets/SpriteAtlases";
        private bool _showSpriteAtlasAdvanced;
        private int _spriteAtlasPadding = 4;
        private int _spriteAtlasMaxSize = 2048;
        private int _spriteAtlasCompressionQuality = 50;
        private List<Yes2SDKSpriteAtlasTool.ScanResult> _spriteAtlasScanResults;
        private List<Yes2SDKSpriteAtlasTool.AtlasReport> _spriteAtlasReports;

        // KTX2 tool state
        private Yes2SDKKtx2Tool.Ktx2Preset _ktx2Preset = Yes2SDKKtx2Tool.Ktx2Preset.UASTC_Zstd;
        private int _ktx2UastcQuality = 2;
        private int _ktx2ZstdLevel = 3;
        private int _ktx2Etc1sQuality = 128;
        private int _ktx2Etc1sCompressionLevel = 2;
        private string _toktxPath;
        private bool _toktxChecked;
        private string _toktxVersion;
        private bool _ktxPackageInstalled;
        private bool _ktxPackageChecked;
        private string _ktx2SourceFolder = "Assets/Textures";
        private List<Yes2SDKKtx2Tool.ConversionResult> _ktx2Results;

        // Texture swap tool state
        private Yes2SDKTextureSwapTool.ScanScope _swapScope = Yes2SDKTextureSwapTool.ScanScope.ActiveScene;
        private string _swapPrefabFolder = "Assets/Prefabs";
        private List<Yes2SDKTextureSwapTool.SwapCandidate> _swapCandidates;
        private Yes2SDKTextureSwapTool.SwapReport _swapReport;

        // Screenshot tool state
        private string _screenshotOutputFolder = Yes2SDKScreenshotTool.DefaultOutputPath;
        private int _screenshotNextIndex = 1;
        private bool[] _screenshotSlotExists = new bool[Yes2SDKScreenshotTool.MaxScreenshots];
        private bool _screenshotRebinding;

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
            _showDebugLogs = EditorPrefs.GetBool("Yes2SDK_ShowDebugLogs", true);
            _selectedTab = EditorPrefs.GetInt("Yes2SDK_SelectedTab", 0);

            // Load utility preferences
            _spriteAtlasSourceFolder = EditorPrefs.GetString("Yes2SDK_SpriteAtlasSource", "Assets/Sprites");
            _spriteAtlasOutputDir = EditorPrefs.GetString("Yes2SDK_SpriteAtlasOutput", "Assets/SpriteAtlases");
            _ktx2SourceFolder = EditorPrefs.GetString("Yes2SDK_Ktx2Source", "Assets/Textures");
            _ktx2Preset = (Yes2SDKKtx2Tool.Ktx2Preset)EditorPrefs.GetInt("Yes2SDK_Ktx2Preset", 0);

            // Screenshot tool preferences
            _screenshotOutputFolder = Yes2SDKScreenshotTool.GetOutputFolder();
            _screenshotNextIndex = Yes2SDKScreenshotTool.GetNextIndex();
            RefreshScreenshotSlots();

            // Check setup status
            RefreshSetupStatus();

            // Detect KTX2 prerequisites on window open
            RefreshKtx2Status();
        }

        private void RefreshKtx2Status()
        {
            _toktxPath = Yes2SDKKtx2Tool.FindToktx();
            if (_toktxPath != null)
                _toktxVersion = Yes2SDKKtx2Tool.ValidateToktx(_toktxPath);
            _toktxChecked = true;

            _ktxPackageInstalled = Yes2SDKKtx2Tool.IsKtxPackageInstalled();
            _ktxPackageChecked = true;
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
            DrawHeader();
            EditorGUILayout.Space(10);

            // Show setup section above tabs if not complete
            if (!_isSetupComplete)
            {
                DrawSetupSection();
                EditorGUILayout.Space(10);
            }

            // Tab toolbar
            EditorGUI.BeginChangeCheck();
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabLabels);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt("Yes2SDK_SelectedTab", _selectedTab);
            }

            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedTab)
            {
                case 0:
                    DrawSDKSettingsSection();
                    EditorGUILayout.Space(10);
                    DrawBuildSection();
                    break;
                case 1:
                    DrawSpriteAtlasUtility();
                    EditorGUILayout.Space(10);
                    DrawKtx2Utility();
                    break;
                case 2:
                    DrawScreenshotTool();
                    EditorGUILayout.Space(10);
                    DrawToolsSection();
                    break;
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.EndScrollView();

            DrawFooter();
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

        private void DrawSDKSettingsSection()
        {
            EditorGUILayout.LabelField("SDK Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _showDebugLogs = EditorGUILayout.Toggle("Show Debug Logs", _showDebugLogs);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("Yes2SDK_ShowDebugLogs", _showDebugLogs);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Enable detailed logging in the Console for debugging purposes.",
                MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildSection()
        {
            EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);

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

        // ─── Screenshot Tool ──────────────────────────────────────────

        private void RefreshScreenshotSlots()
        {
            for (int i = 0; i < Yes2SDKScreenshotTool.MaxScreenshots; i++)
            {
                _screenshotSlotExists[i] = Yes2SDKScreenshotTool.SlotExists(_screenshotOutputFolder, i + 1);
            }
        }

        private void DrawScreenshotTool()
        {
            // Always sync from disk/EditorPrefs so keyboard shortcut captures are reflected
            RefreshScreenshotSlots();
            _screenshotNextIndex = Yes2SDKScreenshotTool.GetNextIndex();

            EditorGUILayout.LabelField("Poki Screenshots", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Capture Game View screenshots for Poki store page.\n" +
                "Saves two sizes per slot: 800\u00d7480 and 100\u00d756 (JPG).",
                MessageType.Info);

            EditorGUILayout.Space(5);

            // Output folder
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _screenshotOutputFolder = EditorGUILayout.TextField("Output Folder", _screenshotOutputFolder);
            if (EditorGUI.EndChangeCheck())
            {
                Yes2SDKScreenshotTool.SetOutputFolder(_screenshotOutputFolder);
                RefreshScreenshotSlots();
            }
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.SaveFolderPanel("Select Screenshot Output Folder", _screenshotOutputFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string projectPath = Directory.GetParent(Application.dataPath).FullName;
                    if (path.StartsWith(projectPath))
                        path = path.Substring(projectPath.Length + 1);
                    _screenshotOutputFolder = path;
                    Yes2SDKScreenshotTool.SetOutputFolder(_screenshotOutputFolder);
                    RefreshScreenshotSlots();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Slot indicators with preview + delete
            for (int i = 0; i < Yes2SDKScreenshotTool.MaxScreenshots; i++)
            {
                bool exists = _screenshotSlotExists[i];
                EditorGUILayout.BeginHorizontal();

                // Slot label
                string status = exists ? "\u2713" : "\u2014";
                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = exists ? FontStyle.Bold : FontStyle.Normal
                };
                if (exists)
                    labelStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                GUILayout.Label($"  {i + 1}.jpg  {status}", labelStyle, GUILayout.Width(80));

                // Preview button
                EditorGUI.BeginDisabledGroup(!exists);
                if (GUILayout.Button("Preview", GUILayout.Width(60)))
                {
                    string path = Yes2SDKScreenshotTool.GetLargePath(_screenshotOutputFolder, i + 1);
                    EditorUtility.OpenWithDefaultApp(path);
                }
                EditorGUI.EndDisabledGroup();

                // Delete button
                EditorGUI.BeginDisabledGroup(!exists);
                if (GUILayout.Button("\u2715", GUILayout.Width(25)))
                {
                    Yes2SDKScreenshotTool.DeleteSlot(_screenshotOutputFolder, i + 1);
                    RefreshScreenshotSlots();
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(3);

            // Next index label
            EditorGUILayout.LabelField("", $"Next: Screenshot {_screenshotNextIndex}", EditorStyles.miniLabel);

            EditorGUILayout.Space(3);

            // Capture button (disabled outside Play Mode)
            string shortcutLabel = Yes2SDKScreenshotTool.GetShortcutLabel();
            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button($"Capture Screenshot ({shortcutLabel})", GUILayout.Height(28)))
            {
                int captureIndex = Yes2SDKScreenshotTool.GetNextAvailableIndex(_screenshotOutputFolder);
                Yes2SDKScreenshotTool.CaptureScreenshot(_screenshotOutputFolder, captureIndex);
                Yes2SDKScreenshotTool.AdvanceNextIndex(captureIndex);
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("", "Enter Play Mode to capture screenshots.", EditorStyles.miniLabel);
            }

            // Shortcut rebind
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Shortcut", GUILayout.Width(EditorGUIUtility.labelWidth));
            if (_screenshotRebinding)
            {
                GUILayout.Label("Press any key...", EditorStyles.boldLabel);
                // Capture the next key event
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
                {
                    Yes2SDKScreenshotTool.SetShortcutBinding(e.keyCode, e.modifiers);
                    _screenshotRebinding = false;
                    e.Use();
                    Repaint();
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(55)))
                {
                    _screenshotRebinding = false;
                }
            }
            else
            {
                GUILayout.Label(shortcutLabel, GUILayout.Width(100));
                if (GUILayout.Button("Rebind", GUILayout.Width(55)))
                {
                    _screenshotRebinding = true;
                }
                if (GUILayout.Button("Clear", GUILayout.Width(45)))
                {
                    Yes2SDKScreenshotTool.SetShortcutBinding(KeyCode.None, 0);
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Open Folder / Clear All
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Folder"))
            {
                string resolved = Yes2SDKScreenshotTool.ResolveOutputFolder(_screenshotOutputFolder);
                if (!Directory.Exists(resolved))
                    Directory.CreateDirectory(resolved);
                EditorUtility.RevealInFinder(resolved);
            }
            if (GUILayout.Button("Clear All"))
            {
                if (EditorUtility.DisplayDialog("Yes2SDK",
                    "Delete all screenshot files and reset index to 1?",
                    "Clear", "Cancel"))
                {
                    Yes2SDKScreenshotTool.ClearAll(_screenshotOutputFolder);
                    _screenshotNextIndex = 1;
                    RefreshScreenshotSlots();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawToolsSection()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Coming Soon", EditorStyles.centeredGreyMiniLabel);
            GUI.enabled = false;
            GUILayout.Button("Letterbox (Portrait Mode)");
            GUILayout.Button("Safe Area Handler");
            GUILayout.Button("Build Size Analyzer");
            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        // ─── Sprite Atlas Utility ───────────────────────────────────────

        private void DrawSpriteAtlasUtility()
        {
            EditorGUILayout.LabelField("Sprite Atlas Automation", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Scan for loose sprites and create WebGL-optimized Sprite Atlases.\n" +
                "Reduces draw calls and texture overhead for smaller, faster builds.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            // Source folder
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _spriteAtlasSourceFolder = EditorGUILayout.TextField("Source Folder", _spriteAtlasSourceFolder);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString("Yes2SDK_SpriteAtlasSource", _spriteAtlasSourceFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Sprite Source Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.Contains(Application.dataPath))
                {
                    _spriteAtlasSourceFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    EditorPrefs.SetString("Yes2SDK_SpriteAtlasSource", _spriteAtlasSourceFolder);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Output directory
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _spriteAtlasOutputDir = EditorGUILayout.TextField("Output Directory", _spriteAtlasOutputDir);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString("Yes2SDK_SpriteAtlasOutput", _spriteAtlasOutputDir);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Atlas Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.Contains(Application.dataPath))
                {
                    _spriteAtlasOutputDir = "Assets" + path.Substring(Application.dataPath.Length);
                    EditorPrefs.SetString("Yes2SDK_SpriteAtlasOutput", _spriteAtlasOutputDir);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Advanced settings
            _showSpriteAtlasAdvanced = EditorGUILayout.Foldout(_showSpriteAtlasAdvanced, "Advanced Settings");
            if (_showSpriteAtlasAdvanced)
            {
                EditorGUI.indentLevel++;
                _spriteAtlasPadding = EditorGUILayout.IntSlider("Padding", _spriteAtlasPadding, 1, 8);
                _spriteAtlasMaxSize = EditorGUILayout.IntPopup("Max Texture Size",
                    _spriteAtlasMaxSize,
                    new[] { "512", "1024", "2048", "4096" },
                    new[] { 512, 1024, 2048, 4096 });
                _spriteAtlasCompressionQuality = EditorGUILayout.IntSlider("Crunch Quality", _spriteAtlasCompressionQuality, 0, 100);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Scan button
            if (GUILayout.Button("Scan for Loose Sprites", GUILayout.Height(25)))
            {
                _spriteAtlasScanResults = Yes2SDKSpriteAtlasTool.ScanForLooseSprites(
                    new[] { _spriteAtlasSourceFolder });
                _spriteAtlasReports = null;
            }

            // Scan results
            if (_spriteAtlasScanResults != null)
            {
                EditorGUILayout.Space(3);

                if (_spriteAtlasScanResults.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No loose sprites found. All sprites are already in atlases, or no sprites exist in the source folder.",
                        MessageType.Info);
                }
                else
                {
                    int totalSprites = _spriteAtlasScanResults.Sum(r => r.spritePaths.Count);
                    EditorGUILayout.LabelField(
                        $"Found {totalSprites} loose sprites in {_spriteAtlasScanResults.Count} folders:",
                        EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    foreach (var result in _spriteAtlasScanResults)
                    {
                        EditorGUILayout.LabelField($"{result.folderName}/", $"{result.spritePaths.Count} sprites");
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(5);

                    // Create button
                    GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                    if (GUILayout.Button($"Create {_spriteAtlasScanResults.Count} Atlases", GUILayout.Height(28)))
                    {
                        var settings = new Yes2SDKSpriteAtlasTool.Settings
                        {
                            padding = _spriteAtlasPadding,
                            maxTextureSize = _spriteAtlasMaxSize,
                            compressionQuality = _spriteAtlasCompressionQuality
                        };

                        _spriteAtlasReports = Yes2SDKSpriteAtlasTool.CreateAtlases(
                            _spriteAtlasScanResults, settings, _spriteAtlasOutputDir);

                        _spriteAtlasScanResults = null;
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            // Creation report
            if (_spriteAtlasReports != null && _spriteAtlasReports.Count > 0)
            {
                EditorGUILayout.Space(3);

                int created = _spriteAtlasReports.Count(r => r.created && !r.alreadyExisted);
                int updated = _spriteAtlasReports.Count(r => r.created && r.alreadyExisted);
                int totalSprites = _spriteAtlasReports.Sum(r => r.spriteCount);

                EditorGUILayout.HelpBox(
                    $"Done! {created} atlases created, {updated} updated.\n" +
                    $"{totalSprites} sprites packed. Estimated draw call savings: ~{Mathf.Max(0, totalSprites - _spriteAtlasReports.Count)}.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        // ─── KTX2 Texture Compression ───────────────────────────────────

        private void DrawKtx2Utility()
        {
            EditorGUILayout.LabelField("KTX2 Texture Compression", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Convert textures to KTX2 (Basis Universal). Transcodes at runtime " +
                "to the optimal GPU format per device (BC7 desktop, ETC2/ASTC mobile).\n" +
                "Smaller on disk than ETC2 Crunched, universal browser support from a single build.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            // Prerequisites
            DrawKtx2Prerequisites();

            bool ready = _toktxPath != null && _ktxPackageInstalled;

            EditorGUI.BeginDisabledGroup(!ready);

            EditorGUILayout.Space(5);

            // Source folder
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _ktx2SourceFolder = EditorGUILayout.TextField("Source Folder", _ktx2SourceFolder);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString("Yes2SDK_Ktx2Source", _ktx2SourceFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Texture Source Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.Contains(Application.dataPath))
                {
                    _ktx2SourceFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    EditorPrefs.SetString("Yes2SDK_Ktx2Source", _ktx2SourceFolder);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("", "Point to your original textures (PNG/JPG), not atlas output folders.", EditorStyles.miniLabel);

            // Preset
            EditorGUI.BeginChangeCheck();
            _ktx2Preset = (Yes2SDKKtx2Tool.Ktx2Preset)EditorGUILayout.EnumPopup("Compression Preset", _ktx2Preset);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetInt("Yes2SDK_Ktx2Preset", (int)_ktx2Preset);

            // Preset-specific settings
            EditorGUI.indentLevel++;
            if (_ktx2Preset == Yes2SDKKtx2Tool.Ktx2Preset.UASTC_Zstd)
            {
                _ktx2UastcQuality = EditorGUILayout.IntSlider("UASTC Quality", _ktx2UastcQuality, 0, 4);
                EditorGUILayout.LabelField("", "0 = fastest encode, lowest quality.  4 = slowest encode, best quality.", EditorStyles.miniLabel);

                _ktx2ZstdLevel = EditorGUILayout.IntSlider("Zstd Level", _ktx2ZstdLevel, 1, 22);
                EditorGUILayout.LabelField("", "1 = fastest, larger file.  22 = slowest, smallest file. 3-6 recommended.", EditorStyles.miniLabel);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("", "UASTC: Near-lossless quality, fast GPU transcode. Best for most textures.", EditorStyles.miniLabel);
            }
            else
            {
                _ktx2Etc1sQuality = EditorGUILayout.IntSlider("ETC1S Quality", _ktx2Etc1sQuality, 1, 255);
                EditorGUILayout.LabelField("", "1 = smallest file, most artifacts.  255 = largest file, best quality.", EditorStyles.miniLabel);

                _ktx2Etc1sCompressionLevel = EditorGUILayout.IntSlider("Compression Level", _ktx2Etc1sCompressionLevel, 0, 5);
                EditorGUILayout.LabelField("", "0 = fastest encode.  5 = slowest encode, best compression ratio.", EditorStyles.miniLabel);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("", "ETC1S: Maximum compression, lower quality. Best for color/photo textures.", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5);

            // Convert button
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Convert All Textures in Folder", GUILayout.Height(28)))
            {
                var textures = Yes2SDKKtx2Tool.FindTexturesInFolder(_ktx2SourceFolder);
                if (textures.Length == 0)
                {
                    EditorUtility.DisplayDialog("Yes2SDK",
                        $"No textures found in {_ktx2SourceFolder}.",
                        "OK");
                }
                else
                {
                    bool proceed = EditorUtility.DisplayDialog("Yes2SDK",
                        $"Convert {textures.Length} textures to KTX2?\n\n" +
                        $"Preset: {_ktx2Preset}\n" +
                        $"Output: StreamingAssets/ktx2/",
                        "Convert", "Cancel");

                    if (proceed)
                    {
                        var settings = new Yes2SDKKtx2Tool.Settings
                        {
                            preset = _ktx2Preset,
                            uastcQuality = _ktx2UastcQuality,
                            zstdLevel = _ktx2ZstdLevel,
                            etc1sQuality = _ktx2Etc1sQuality,
                            etc1sCompressionLevel = _ktx2Etc1sCompressionLevel
                        };

                        _ktx2Results = Yes2SDKKtx2Tool.BatchConvert(textures, settings, _toktxPath);
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUI.EndDisabledGroup();

            // Conversion report
            if (_ktx2Results != null && _ktx2Results.Count > 0)
            {
                EditorGUILayout.Space(3);

                int succeeded = _ktx2Results.Count(r => r.success);
                int failed = _ktx2Results.Count(r => !r.success);
                long totalOriginal = _ktx2Results.Where(r => r.success).Sum(r => r.originalSize);
                long totalKtx2 = _ktx2Results.Where(r => r.success).Sum(r => r.ktx2Size);
                float avgRatio = totalKtx2 > 0 ? (float)totalOriginal / totalKtx2 : 0;

                var msg = $"Converted {succeeded}/{_ktx2Results.Count} textures.\n";
                if (succeeded > 0)
                {
                    msg += $"Avg compression: {avgRatio:F1}x. " +
                           $"Saved: {FormatBytes(totalOriginal - totalKtx2)}.";
                }
                if (failed > 0)
                    msg += $"\n{failed} failed — check Console for details.";

                EditorGUILayout.HelpBox(msg,
                    failed > 0 ? MessageType.Warning : MessageType.Info);
            }

            // ─── Swap Texture References ────────────────────────────────
            EditorGUILayout.Space(10);
            if (_ktxPackageInstalled)
            {
                DrawSwapTextureSection();
            }
            else
            {
                EditorGUILayout.LabelField("Swap Texture References", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Install com.unity.cloud.ktx to enable runtime KTX2 loading and texture swapping.",
                    MessageType.Info);
                if (GUILayout.Button("Install com.unity.cloud.ktx"))
                {
                    Yes2SDKKtx2Tool.InstallKtxPackage();
                    RefreshKtx2Status();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSwapTextureSection()
        {
            EditorGUILayout.LabelField("Swap Texture References", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Replace texture references in scenes/prefabs with KTX2 runtime loaders.\n" +
                "Original textures are cleared so they're excluded from Build.data, " +
                "reducing initial download size.",
                MessageType.Info);

            EditorGUILayout.Space(3);

            // Scope selection
            _swapScope = (Yes2SDKTextureSwapTool.ScanScope)EditorGUILayout.EnumPopup("Scan Scope", _swapScope);

            // Prefab folder picker (only shown for PrefabsInFolder scope)
            if (_swapScope == Yes2SDKTextureSwapTool.ScanScope.PrefabsInFolder)
            {
                EditorGUILayout.BeginHorizontal();
                _swapPrefabFolder = EditorGUILayout.TextField("Prefab Folder", _swapPrefabFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var path = EditorUtility.OpenFolderPanel("Select Prefab Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path) && path.Contains(Application.dataPath))
                    {
                        _swapPrefabFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(3);

            // Scan button
            if (GUILayout.Button("Scan", GUILayout.Height(25)))
            {
                _swapReport = null;

                switch (_swapScope)
                {
                    case Yes2SDKTextureSwapTool.ScanScope.ActiveScene:
                        _swapCandidates = Yes2SDKTextureSwapTool.ScanActiveScene();
                        break;
                    case Yes2SDKTextureSwapTool.ScanScope.AllBuildScenes:
                        _swapCandidates = Yes2SDKTextureSwapTool.ScanAllBuildScenes();
                        break;
                    case Yes2SDKTextureSwapTool.ScanScope.PrefabsInFolder:
                        _swapCandidates = Yes2SDKTextureSwapTool.ScanPrefabs(_swapPrefabFolder);
                        break;
                }
            }

            // Scan results
            if (_swapCandidates != null)
            {
                EditorGUILayout.Space(3);

                if (_swapCandidates.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No swappable textures found. Either all textures are already swapped, " +
                        "or no matching KTX2 files exist.",
                        MessageType.Info);
                }
                else
                {
                    int ready = _swapCandidates.Count(c => c.ktx2Exists && !c.inAtlas);
                    int missingKtx2 = _swapCandidates.Count(c => !c.ktx2Exists && !c.inAtlas);
                    int inAtlas = _swapCandidates.Count(c => c.inAtlas);

                    var summary = $"Found {_swapCandidates.Count} texture references:";
                    summary += $"\n  {ready} ready to swap";
                    if (missingKtx2 > 0)
                        summary += $"\n  {missingKtx2} missing KTX2 (convert first)";
                    if (inAtlas > 0)
                        summary += $"\n  {inAtlas} skipped (in SpriteAtlas)";

                    // Component type breakdown
                    var byType = _swapCandidates.Where(c => c.ktx2Exists && !c.inAtlas)
                        .GroupBy(c => c.componentType);
                    foreach (var group in byType)
                    {
                        summary += $"\n  {group.Key}: {group.Count()}";
                    }

                    EditorGUILayout.HelpBox(summary,
                        missingKtx2 > 0 ? MessageType.Warning : MessageType.Info);

                    if (missingKtx2 > 0)
                    {
                        EditorGUILayout.LabelField("",
                            "Convert missing textures using the tool above before swapping.",
                            EditorStyles.miniLabel);
                    }

                    EditorGUILayout.Space(3);

                    // Swap button
                    EditorGUI.BeginDisabledGroup(ready == 0);
                    GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                    if (GUILayout.Button($"Swap {ready} Textures", GUILayout.Height(28)))
                    {
                        bool proceed = EditorUtility.DisplayDialog("Yes2SDK",
                            $"This will:\n" +
                            $"- Add Yes2SDKKtx2Image to {ready} GameObjects\n" +
                            $"- Clear original texture references\n\n" +
                            "Scenes/prefabs will be saved. This can be undone with Ctrl+Z before saving.",
                            "Swap", "Cancel");

                        if (proceed)
                        {
                            _swapReport = Yes2SDKTextureSwapTool.PerformSwap(_swapCandidates);
                            _swapCandidates = null;
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUI.EndDisabledGroup();
                }
            }

            // Swap report
            if (_swapReport != null)
            {
                EditorGUILayout.Space(3);

                var reportMsg = $"Swap complete: {_swapReport.swapped} textures swapped.";
                if (_swapReport.skippedMissingKtx2 > 0)
                    reportMsg += $"\n{_swapReport.skippedMissingKtx2} skipped (no KTX2 file).";
                if (_swapReport.skippedInAtlas > 0)
                    reportMsg += $"\n{_swapReport.skippedInAtlas} skipped (in SpriteAtlas).";
                if (_swapReport.skippedAlreadySwapped > 0)
                    reportMsg += $"\n{_swapReport.skippedAlreadySwapped} skipped (already swapped).";

                EditorGUILayout.HelpBox(reportMsg, MessageType.Info);
            }
        }

        private void DrawKtx2Prerequisites()
        {

            EditorGUILayout.BeginHorizontal();
            var packageIcon = _ktxPackageInstalled ? "●" : "○";
            var packageColor = _ktxPackageInstalled ? "green" : "yellow";
            var packageStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true
            };
            EditorGUILayout.LabelField(
                $"<color={packageColor}>{packageIcon}</color> com.unity.cloud.ktx: " +
                (_ktxPackageInstalled ? "Installed" : "Not Installed"),
                packageStyle);
            if (!_ktxPackageInstalled)
            {
                if (GUILayout.Button("Install", GUILayout.Width(60)))
                {
                    Yes2SDKKtx2Tool.InstallKtxPackage();
                    RefreshKtx2Status();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var toktxIcon = _toktxPath != null ? "●" : "○";
            var toktxColor = _toktxPath != null ? "green" : "yellow";
            var toktxStyle = new GUIStyle(EditorStyles.label) { richText = true };

            if (_toktxPath != null)
            {
                EditorGUILayout.LabelField(
                    $"<color={toktxColor}>{toktxIcon}</color> toktx: Found" +
                    (!string.IsNullOrEmpty(_toktxVersion) ? $" ({_toktxVersion})" : ""),
                    toktxStyle);
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"<color={toktxColor}>{toktxIcon}</color> toktx: Not Found",
                    toktxStyle);

                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    var path = EditorUtility.OpenFilePanel("Locate toktx", "", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var version = Yes2SDKKtx2Tool.ValidateToktx(path);
                        if (version != null)
                        {
                            Yes2SDKKtx2Tool.SaveToktxPath(path);
                            _toktxPath = path;
                            _toktxVersion = version;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Yes2SDK",
                                "The selected file is not a valid toktx executable.",
                                "OK");
                        }
                    }
                }
                if (GUILayout.Button("Detect", GUILayout.Width(50)))
                {
                    RefreshKtx2Status();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_toktxPath == null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                if (GUILayout.Button("Download KTX-Software", EditorStyles.linkLabel))
                {
                    Application.OpenURL(Yes2SDKKtx2Tool.KtxSoftwareReleasesUrl);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
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
