using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

namespace Yes2SDK.Editor
{
    [InitializeOnLoad]
    public static class Yes2SDKScreenshotTool
    {
        public const int LargeWidth = 800;
        public const int LargeHeight = 480;
        public const int SmallWidth = 100;
        public const int SmallHeight = 56;
        public const int MaxScreenshots = 4;
        public const string DefaultOutputPath = "Builds/Poki/screenshots";

        private const string PrefKeyOutputFolder = "Yes2SDK_ScreenshotOutputFolder";
        private const string PrefKeyNextIndex = "Yes2SDK_ScreenshotNextIndex";
        private const string PrefKeyKeyCode = "Yes2SDK_ScreenshotKeyCode";
        private const string PrefKeyModifiers = "Yes2SDK_ScreenshotModifiers";

        // ─── Shortcut Binding ────────────────────────────────────────

        static Yes2SDKScreenshotTool()
        {
            // Hook into the editor's global event handler so the shortcut
            // works even when the Game View has focus during Play Mode.
            var fi = typeof(EditorApplication).GetField(
                "globalEventHandler",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (fi != null)
            {
                var handler = (EditorApplication.CallbackFunction)fi.GetValue(null);
                handler += OnGlobalEvent;
                fi.SetValue(null, handler);
            }
        }

        private static void OnGlobalEvent()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            KeyCode key = GetShortcutKeyCode();
            if (key == KeyCode.None)
                return;

            EventModifiers mods = GetShortcutModifiers();
            // Mask out CapsLock / FunctionKey / Numeric so they don't block the match
            EventModifiers currentMods = e.modifiers & (EventModifiers.Control | EventModifiers.Shift | EventModifiers.Alt | EventModifiers.Command);

            if (e.keyCode == key && currentMods == mods)
            {
                if (!EditorApplication.isPlaying)
                    return;

                e.Use();

                string folder = GetOutputFolder();
                int index = GetNextAvailableIndex(folder);

                CaptureScreenshot(folder, index);
                AdvanceNextIndex(index);
            }
        }

        public static KeyCode GetShortcutKeyCode()
        {
            return (KeyCode)EditorPrefs.GetInt(PrefKeyKeyCode, (int)KeyCode.F12);
        }

        public static EventModifiers GetShortcutModifiers()
        {
            return (EventModifiers)EditorPrefs.GetInt(PrefKeyModifiers, 0);
        }

        public static void SetShortcutBinding(KeyCode key, EventModifiers modifiers)
        {
            EditorPrefs.SetInt(PrefKeyKeyCode, (int)key);
            // Only store meaningful modifiers
            EditorPrefs.SetInt(PrefKeyModifiers,
                (int)(modifiers & (EventModifiers.Control | EventModifiers.Shift | EventModifiers.Alt | EventModifiers.Command)));
        }

        public static string GetShortcutLabel()
        {
            KeyCode key = GetShortcutKeyCode();
            if (key == KeyCode.None)
                return "None";

            EventModifiers mods = GetShortcutModifiers();
            string label = "";

            if ((mods & EventModifiers.Command) != 0)
                label += Application.platform == RuntimePlatform.OSXEditor ? "Cmd+" : "Ctrl+";
            if ((mods & EventModifiers.Control) != 0 && Application.platform == RuntimePlatform.OSXEditor)
                label += "Ctrl+";
            if ((mods & EventModifiers.Shift) != 0)
                label += "Shift+";
            if ((mods & EventModifiers.Alt) != 0)
                label += "Alt+";

            label += key.ToString();
            return label;
        }

        // ─── EditorPrefs ─────────────────────────────────────────────

        public static string GetOutputFolder()
        {
            return EditorPrefs.GetString(PrefKeyOutputFolder, DefaultOutputPath);
        }

        public static void SetOutputFolder(string path)
        {
            EditorPrefs.SetString(PrefKeyOutputFolder, path);
        }

        public static int GetNextIndex()
        {
            return EditorPrefs.GetInt(PrefKeyNextIndex, 1);
        }

        public static void SetNextIndex(int index)
        {
            EditorPrefs.SetInt(PrefKeyNextIndex, index);
        }

        /// <summary>
        /// Returns the best slot to capture into: first empty slot (1-4),
        /// or falls back to sequential overwrite if all slots are filled.
        /// </summary>
        public static int GetNextAvailableIndex(string outputFolder)
        {
            // Prioritize empty slots
            for (int i = 1; i <= MaxScreenshots; i++)
            {
                if (!SlotExists(outputFolder, i))
                    return i;
            }

            // All filled — use the sequential index for overwrite
            return GetNextIndex();
        }

        /// <summary>
        /// Advances the sequential index for the overwrite-when-full case.
        /// </summary>
        public static void AdvanceNextIndex(int currentIndex)
        {
            int next = currentIndex >= MaxScreenshots ? 1 : currentIndex + 1;
            SetNextIndex(next);
        }

        /// <summary>
        /// Resolves a potentially relative output folder to an absolute path
        /// rooted at the Unity project directory.
        /// </summary>
        public static string ResolveOutputFolder(string outputFolder)
        {
            if (Path.IsPathRooted(outputFolder))
                return outputFolder;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, outputFolder);
        }

        // ─── Slot Queries ────────────────────────────────────────────

        public static bool SlotExists(string outputFolder, int index)
        {
            string resolved = ResolveOutputFolder(outputFolder);
            string largePath = Path.Combine(resolved, $"{index}.jpg");
            string smallPath = Path.Combine(resolved, $"{index}-small.jpg");
            return File.Exists(largePath) && File.Exists(smallPath);
        }

        public static string GetLargePath(string outputFolder, int index)
        {
            return Path.Combine(ResolveOutputFolder(outputFolder), $"{index}.jpg");
        }

        public static void DeleteSlot(string outputFolder, int index)
        {
            string resolved = ResolveOutputFolder(outputFolder);
            string largePath = Path.Combine(resolved, $"{index}.jpg");
            string smallPath = Path.Combine(resolved, $"{index}-small.jpg");
            if (File.Exists(largePath)) File.Delete(largePath);
            if (File.Exists(smallPath)) File.Delete(smallPath);
            Debug.Log($"[Yes2SDK] Screenshot {index} deleted.");
        }

        public static void ClearAll(string outputFolder)
        {
            string resolved = ResolveOutputFolder(outputFolder);
            for (int i = 1; i <= MaxScreenshots; i++)
            {
                string largePath = Path.Combine(resolved, $"{i}.jpg");
                string smallPath = Path.Combine(resolved, $"{i}-small.jpg");
                if (File.Exists(largePath)) File.Delete(largePath);
                if (File.Exists(smallPath)) File.Delete(smallPath);
            }
            SetNextIndex(1);
            Debug.Log("[Yes2SDK] All screenshots cleared.");
        }

        // ─── Capture ─────────────────────────────────────────────────

        /// <summary>
        /// Captures the Game View and saves resized JPGs. Must be called in Play Mode.
        /// Uses EditorApplication.delayCall to capture after the current frame renders.
        /// </summary>
        public static void CaptureScreenshot(string outputFolder, int index)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Yes2SDK] Screenshot capture requires Play Mode.");
                return;
            }

            if (index < 1 || index > MaxScreenshots)
            {
                Debug.LogError($"[Yes2SDK] Screenshot index must be between 1 and {MaxScreenshots}.");
                return;
            }

            string resolved = ResolveOutputFolder(outputFolder);

            // Defer capture to after the current GUI pass so the Game View has rendered
            EditorApplication.delayCall += () => CaptureImmediate(resolved, index);
        }

        private static void CaptureImmediate(string absoluteFolder, int index)
        {
            if (!EditorApplication.isPlaying)
                return;

            // Ensure output directory exists
            if (!Directory.Exists(absoluteFolder))
            {
                Directory.CreateDirectory(absoluteFolder);
            }

            // Render cameras to a RenderTexture at the game's actual resolution,
            // bypassing ScreenCapture which includes letterbox bars.
            Texture2D capture = CaptureGameCameras(Screen.width, Screen.height);
            if (capture == null)
            {
                Debug.LogError("[Yes2SDK] Failed to capture screenshot. No active cameras found.");
                return;
            }

            try
            {
                // Large: 800x480
                Texture2D large = ResizeTexture(capture, LargeWidth, LargeHeight);
                byte[] largeJpg = large.EncodeToJPG(90);
                string largePath = Path.Combine(absoluteFolder, $"{index}.jpg");
                File.WriteAllBytes(largePath, largeJpg);
                Object.DestroyImmediate(large);

                // Small: 100x56
                Texture2D small = ResizeTexture(capture, SmallWidth, SmallHeight);
                byte[] smallJpg = small.EncodeToJPG(85);
                string smallPath = Path.Combine(absoluteFolder, $"{index}-small.jpg");
                File.WriteAllBytes(smallPath, smallJpg);
                Object.DestroyImmediate(small);

                Debug.Log($"[Yes2SDK] Screenshot {index} saved to {absoluteFolder}/");
            }
            finally
            {
                Object.DestroyImmediate(capture);
            }
        }

        /// <summary>
        /// Renders all active cameras (sorted by depth) to a RenderTexture,
        /// capturing only the game content without letterbox bars.
        /// Screen Space - Camera UI is included; Screen Space - Overlay is not.
        /// </summary>
        private static Texture2D CaptureGameCameras(int width, int height)
        {
            Camera[] cameras = Camera.allCameras;
            if (cameras.Length == 0)
                return null;

            // Sort by depth so cameras render in correct order
            System.Array.Sort(cameras, (a, b) => a.depth.CompareTo(b.depth));

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            foreach (Camera cam in cameras)
            {
                RenderTexture originalTarget = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = originalTarget;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        // ─── Menu Item (fallback) ────────────────────────────────────

        [MenuItem("Yes2SDK/Capture Screenshot")]
        private static void MenuCapture()
        {
            string folder = GetOutputFolder();
            int index = GetNextAvailableIndex(folder);

            CaptureScreenshot(folder, index);
            AdvanceNextIndex(index);
        }

        [MenuItem("Yes2SDK/Capture Screenshot", true)]
        private static bool ValidateMenuCapture()
        {
            return EditorApplication.isPlaying;
        }
    }
}
