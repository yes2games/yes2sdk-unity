using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
        private const string AndroidIdentifier = "com.yes2games.yes2sdk.ciproof";
        private const string TemplateName = "Yes2SDK-SuperSDK";
        private const string TemplateSetting = "PROJECT:" + TemplateName;
        private const string ScenePath = "Assets/Smoke/Smoke.unity";
        private const string ControlPlaneAssembly = "Yes2SDK.ControlPlane";
        private const string MobileMaxDprVariable = "MOBILE_MAX_DPR";

        public static void BuildWebGL()
        {
            RunAndExit("WebGL package smoke", Run);
        }

        public static void BuildAndroid()
        {
            RunAndExit("Android IL2CPP control plane proof build", () =>
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    throw new Exception(
                        "active build target is " + EditorUserBuildSettings.activeBuildTarget +
                        "; this build must run in an Editor started with -buildTarget Android");
                }

                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidIdentifier);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                EditorUserBuildSettings.buildAppBundle = false;

                CreateProofScene();
                string outputPath = CustomBuildPath() ?? Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "build", "Android", "Yes2SDKProof.apk"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                BuildScene(outputPath, BuildTarget.Android);
                if (!File.Exists(outputPath))
                {
                    throw new Exception("Android build reported success but produced no APK at " + outputPath);
                }
                Debug.Log("[smoke] built Android IL2CPP ARM64+x86_64 player at High stripping: " + outputPath);
            });
        }

        public static void CompileWithoutControlPlane()
        {
            RunAndExit("WebGL compile without the control plane", () =>
            {
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == ControlPlaneAssembly))
                {
                    throw new Exception(ControlPlaneAssembly + " is loaded in the editor without YES2SDK_CONTROL_PLANE");
                }
                if (CompileWebGLPlayerScripts().Contains(ControlPlaneAssembly))
                {
                    throw new Exception(ControlPlaneAssembly + " compiled into the WebGL player without YES2SDK_CONTROL_PLANE");
                }
            });
        }

        private static void RunAndExit(string name, Action body)
        {
            try
            {
                body();
                Debug.Log("[smoke] " + name + " passed.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[smoke] " + name + " failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static string[] CompileWebGLPlayerScripts()
        {
            ScriptCompilationResult result = PlayerBuildInterface.CompilePlayerScripts(
                new ScriptCompilationSettings { target = BuildTarget.WebGL, group = BuildTargetGroup.WebGL },
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "SmokePlayerScripts")));
            string[] assemblies = result.assemblies.Select(Path.GetFileNameWithoutExtension).ToArray();
            if (assemblies.Length == 0)
            {
                throw new Exception("WebGL player scripts did not compile");
            }
            Debug.Log("[smoke] WebGL player assemblies: " + string.Join(", ", assemblies));
            return assemblies;
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
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            AssetDatabase.Refresh();

            CreateProofScene();

            string outputPath = CustomBuildPath() ?? Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "build", "WebGL"));
            Directory.CreateDirectory(outputPath);

            // Yes2SDKBuildGuard runs as a preprocess callback here, so a missing or
            // wrong template fails the build rather than shipping a player with no
            // JS bridge wired up.
            BuildScene(outputPath, BuildTarget.WebGL);

            // A "succeeded" build with no loader is not a player. Unity writes the
            // loader under Build/ for every WebGL output layout this lane uses.
            string[] loaders = Directory.Exists(outputPath)
                ? Directory.GetFiles(outputPath, "*.loader.js", SearchOption.AllDirectories)
                : Array.Empty<string>();
            if (loaders.Length == 0)
            {
                throw new Exception("WebGL build reported success but produced no *.loader.js under " + outputPath);
            }
            Debug.Log("[smoke] built WebGL player at High stripping: " + loaders.First());

            string builtIndex = Path.Combine(outputPath, "index.html");
            if (File.ReadAllText(builtIndex).Contains("devicePixelRatio"))
            {
                throw new Exception("MOBILE_MAX_DPR is unset but " + builtIndex + " still sets devicePixelRatio");
            }

            try
            {
                PlayerSettings.SetTemplateCustomValue(MobileMaxDprVariable, "abc");
                BuildResult invalid = BuildPipeline.BuildPlayer(
                    new[] { ScenePath }, outputPath, BuildTarget.WebGL, BuildOptions.None).summary.result;
                if (invalid != BuildResult.Failed)
                {
                    throw new Exception("MOBILE_MAX_DPR 'abc' build result is " + invalid + ", expected the build guard to fail it");
                }

                PlayerSettings.SetTemplateCustomValue(MobileMaxDprVariable, "1.4");
                BuildResult capped = BuildPipeline.BuildPlayer(
                    new[] { ScenePath }, outputPath, BuildTarget.WebGL, BuildOptions.None).summary.result;
                if (capped != BuildResult.Succeeded)
                {
                    throw new Exception("MOBILE_MAX_DPR '1.4' build result is " + capped);
                }
                if (!File.ReadAllText(builtIndex).Contains("Math.min(1.4, window.devicePixelRatio)"))
                {
                    throw new Exception("MOBILE_MAX_DPR is 1.4 but " + builtIndex + " does not cap devicePixelRatio at 1.4");
                }
                Debug.Log("[smoke] MOBILE_MAX_DPR: 'abc' failed the build, '1.4' capped devicePixelRatio");
            }
            finally
            {
                PlayerSettings.SetTemplateCustomValue(MobileMaxDprVariable, "");
            }

            if (!CompileWebGLPlayerScripts().Contains(ControlPlaneAssembly))
            {
                throw new Exception(ControlPlaneAssembly + " did not compile for WebGL with YES2SDK_CONTROL_PLANE defined");
            }
        }

        private static void CreateProofScene()
        {
            // Build the scene here rather than committing a .unity file: its
            // serialized form differs between the floor and the current lane, and a
            // scene checked in under one of them is a diff waiting to happen.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
#if YES2SDK_CONTROL_PLANE
            new GameObject(nameof(ControlPlaneNativeProof)).AddComponent<ControlPlaneNativeProof>();
#endif
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new Exception("could not save the smoke scene to " + ScenePath);
            }
        }

        private static void BuildScene(string outputPath, BuildTarget target)
        {
            BuildReport report = BuildPipeline.BuildPlayer(new[] { ScenePath }, outputPath, target, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception(
                    target + " player build result is " + report.summary.result + " with " +
                    report.summary.totalErrors + " error(s)");
            }
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
