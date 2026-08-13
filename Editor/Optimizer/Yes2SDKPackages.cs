using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Presence and installation of the optional Unity packages the optimizer recommends. One home for
    /// the rule, so a check that reports a package missing and a check that offers to install it agree.
    /// </summary>
    public static class Yes2SDKPackages
    {
        /// <summary>Runtime KTX2 and Basis texture loading. Needed to reference a converted texture.</summary>
        public const string Ktx2 = "com.unity.cloud.ktx";

        /// <summary>On-demand asset loading, which keeps content out of the initial download.</summary>
        public const string Addressables = "com.unity.addressables";

        /// <summary>Heap and asset memory capture, for measuring before cutting.</summary>
        public const string MemoryProfiler = "com.unity.memoryprofiler";

        /// <summary>
        /// True when the package is in this project. Every installed package mounts at
        /// <c>Packages/&lt;id&gt;</c> whether it was added directly or pulled in as a dependency, so this
        /// answers for both without waiting on an asynchronous Package Manager request.
        /// </summary>
        public static bool IsInstalled(string packageId) => AssetDatabase.IsValidFolder("Packages/" + packageId);

        /// <summary>
        /// Asks the Package Manager to add the package. The request is asynchronous and the Package
        /// Manager reports its own progress and failures, so there is nothing to await here.
        /// </summary>
        public static void Install(string packageId)
        {
            Debug.Log("Yes2SDK Optimizer is installing " + packageId + ". The Package Manager reports when it finishes.");
            Client.Add(packageId);
        }
    }
}
