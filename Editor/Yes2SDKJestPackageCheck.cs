using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>What the Jest package check found in the project.</summary>
    [Flags]
    public enum Yes2SDKJestConflict
    {
        None = 0,

        /// <summary>The Jest Unity SDK is installed through the Package Manager.</summary>
        Package = 1 << 0,

        /// <summary>The Jest SDK assembly is compiled into the project, for example from a copy under Assets.</summary>
        Assembly = 1 << 1,

        /// <summary>The init script the Jest package copies into Assets is present and ships in WebGL builds.</summary>
        InitScript = 1 << 2,
    }

    /// <summary>
    /// Warns when Jest's own Unity SDK is in the same project as Yes2SDK. Yes2SDK loads the Jest
    /// SDK itself on Jest, so a second copy from Jest's package can conflict at runtime.
    ///
    /// Warning only, never a build failure. It logs once per Editor session after load, and once
    /// per WebGL build while the Yes2SDK pipeline is enabled.
    /// </summary>
    public class Yes2SDKJestPackageCheck : IPreprocessBuildWithReport
    {
        /// <summary>UPM name of Jest's Unity SDK (github.com/jest-com/jest-unity-sdk).</summary>
        public const string JestPackageId = "com.jest.sdk";

        /// <summary>Name of the runtime assembly that package compiles.</summary>
        public const string JestAssemblyName = "com.jest.sdk";

        /// <summary>The init script Jest's build preprocessor copies into the project.</summary>
        public const string JestInitScriptPath = "Assets/com.jest.sdk/init.jspre";

        private const string SessionWarnedKey = "Yes2SDK.JestPackageWarned";

        // Runs alongside Yes2SDKBuildGuard. A warning has no ordering needs.
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void CheckOnLoad()
        {
            // Defer so the AssetDatabase and package folders are ready.
            EditorApplication.delayCall += WarnOncePerSession;
        }

        private static void WarnOncePerSession()
        {
            if (SessionState.GetBool(SessionWarnedKey, false)) return;

            var message = BuildWarning(DetectInProject());
            if (message == null) return;

            SessionState.SetBool(SessionWarnedKey, true);
            Debug.LogWarning(message);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            // Another platform owns this build, see Yes2SDKPipeline.
            if (!Yes2SDKPipeline.Enabled) return;

            var message = BuildWarning(DetectInProject());
            if (message != null) Debug.LogWarning(message);
        }

        /// <summary>Runs <see cref="Detect"/> against the open project.</summary>
        public static Yes2SDKJestConflict DetectInProject()
        {
            var assemblyNames = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                assemblyNames.Add(assembly.GetName().Name);
            }

            return Detect(AssetDatabase.IsValidFolder, File.Exists, assemblyNames);
        }

        /// <summary>
        /// Looks for Jest's Unity SDK. Every installed package mounts at <c>Packages/&lt;id&gt;</c>
        /// whatever its source (git URL, registry, embedded or local), the assembly catches a copy
        /// placed under Assets, and the init script catches a copy left behind after the package
        /// was removed.
        /// </summary>
        public static Yes2SDKJestConflict Detect(
            Func<string, bool> folderExists,
            Func<string, bool> fileExists,
            IEnumerable<string> loadedAssemblyNames)
        {
            var found = Yes2SDKJestConflict.None;

            if (folderExists("Packages/" + JestPackageId)) found |= Yes2SDKJestConflict.Package;

            foreach (var name in loadedAssemblyNames)
            {
                if (string.Equals(name, JestAssemblyName, StringComparison.Ordinal))
                {
                    found |= Yes2SDKJestConflict.Assembly;
                    break;
                }
            }

            if (fileExists(JestInitScriptPath)) found |= Yes2SDKJestConflict.InitScript;

            return found;
        }

        /// <summary>The warning to log for what was found, or null when nothing was.</summary>
        public static string BuildWarning(Yes2SDKJestConflict found)
        {
            if (found == Yes2SDKJestConflict.None) return null;

            var sb = new StringBuilder();
            sb.Append("[Yes2SDK] Jest's own Unity SDK is in this project. ");
            sb.Append("Yes2SDK already loads the Jest SDK when the game runs on Jest, ");
            sb.Append("and a second copy can conflict with it. Remove it to avoid conflicts:");

            if ((found & (Yes2SDKJestConflict.Package | Yes2SDKJestConflict.Assembly)) != 0)
            {
                sb.Append("\n- Remove the Jest SDK package (").Append(JestPackageId)
                  .Append(") in Window > Package Manager, or delete its folder if it was copied into Assets.");
            }

            if ((found & Yes2SDKJestConflict.InitScript) != 0)
            {
                sb.Append("\n- Delete ").Append(JestInitScriptPath)
                  .Append(" after removing the package. It loads the Jest SDK and is included in WebGL builds.");
            }

            return sb.ToString();
        }
    }
}
