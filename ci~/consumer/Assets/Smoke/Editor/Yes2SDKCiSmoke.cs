using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yes2SDK.Editor;

namespace Yes2Games.CI
{
    /// <summary>
    /// The WebGL package smoke for the current lane (yes2games/yes2sdk-unity#97
    /// section 3). It proves the things a consuming game actually does, in the
    /// order it does them: resolve the package, run the supported installer, end
    /// up with the template the build guard demands, and get a real WebGL player
    /// out of BuildPipeline.BuildPlayer.
    ///
    /// Every failure below exits non-zero. A smoke that logs an error and exits 0
    /// is worse than no smoke, because it turns a red lane green.
    ///
    /// Invoked by game-ci/unity-builder as -executeMethod
    /// Yes2Games.CI.Yes2SDKCiSmoke.BuildWebGL.
    /// </summary>
    public static class Yes2SDKCiSmoke
    {
        private const string PackageName = "com.yes2games.yes2sdk";
        private const string TemplateName = "Yes2SDK-SuperSDK";
        private const string TemplateSetting = "PROJECT:" + TemplateName;
        private const string ScenePath = "Assets/Smoke/Smoke.unity";

        public static void BuildWebGL()
        {
            try
            {
                Run();
                Debug.Log("[smoke] WebGL package smoke passed.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[smoke] WebGL package smoke failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void Run()
        {
            // Resolve. A package that did not resolve leaves no folder behind, and
            // every later step would fail with a less obvious message.
            string packageManifest = "Packages/" + PackageName + "/package.json";
            if (!File.Exists(packageManifest))
            {
                throw new Exception(
                    "package " + PackageName + " did not resolve; no " + packageManifest + " in this project");
            }
            Debug.Log(
                "[smoke] resolved " + PackageName +
                " | SDK SemVer " + global::Yes2SDK.Yes2SDK.Version +
                " | Unity " + Application.unityVersion);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                throw new Exception(
                    "active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    "; this smoke must run in an Editor started with -buildTarget WebGL");
            }

            // The supported installer, not a hand-rolled copy: the point is to prove
            // the path a consuming game is told to use in the README.
            if (!Yes2SDKInstaller.PerformSetup())
            {
                throw new Exception("Yes2SDKInstaller.PerformSetup() returned false");
            }

            string templateIndex = Path.Combine(
                Application.dataPath, "WebGLTemplates", TemplateName, "index.html");
            if (!File.Exists(templateIndex))
            {
                throw new Exception("installer reported success but the template is missing: " + templateIndex);
            }
            if (!Yes2SDKInstaller.IsSetupComplete())
            {
                throw new Exception("Yes2SDKInstaller.IsSetupComplete() is false after a successful setup");
            }

            PlayerSettings.WebGL.template = TemplateSetting;
            AssetDatabase.Refresh();

            // Build the scene here rather than committing a .unity file: its
            // serialized form differs between the floor and the current lane, and a
            // scene checked in under one of them is a diff waiting to happen.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new Exception("could not save the smoke scene to " + ScenePath);
            }

            string outputPath = CustomBuildPath() ?? Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "build", "WebGL"));
            Directory.CreateDirectory(outputPath);

            // Yes2SDKBuildGuard runs as a preprocess callback here, so a missing or
            // wrong template fails the build rather than shipping a player with no
            // JS bridge wired up.
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { ScenePath }, outputPath, BuildTarget.WebGL, BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception(
                    "WebGL player build result is " + report.summary.result + " with " +
                    report.summary.totalErrors + " error(s)");
            }

            // A "succeeded" build with no loader is not a player. Unity writes the
            // loader under Build/ for every WebGL output layout this lane uses.
            string[] loaders = Directory.Exists(outputPath)
                ? Directory.GetFiles(outputPath, "*.loader.js", SearchOption.AllDirectories)
                : Array.Empty<string>();
            if (loaders.Length == 0)
            {
                throw new Exception("WebGL build reported success but produced no *.loader.js under " + outputPath);
            }
            Debug.Log("[smoke] built WebGL player: " + loaders.First());
        }

        /// <summary>
        /// game-ci/unity-builder passes the output directory it expects to find
        /// populated afterwards. Honour it, so the action's own post-build check
        /// agrees with what this method wrote.
        /// </summary>
        private static string CustomBuildPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-customBuildPath")
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
