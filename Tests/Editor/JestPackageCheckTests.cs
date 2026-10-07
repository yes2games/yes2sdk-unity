using System.Collections.Generic;
using NUnit.Framework;
using Yes2SDK.Editor;

namespace Yes2SDK.Tests
{
    /// <summary>Covers detection and the warning text of the Jest package check.</summary>
    public class JestPackageCheckTests
    {
        private static readonly string[] UnrelatedAssemblies = { "UnityEngine", "Yes2SDK.Runtime", "Assembly-CSharp" };

        private static Yes2SDKJestConflict Detect(
            bool packageFolder = false,
            bool initScript = false,
            IEnumerable<string> assemblies = null)
        {
            return Yes2SDKJestPackageCheck.Detect(
                path => packageFolder && path == "Packages/com.jest.sdk",
                path => initScript && path == "Assets/com.jest.sdk/init.jspre",
                assemblies ?? UnrelatedAssemblies);
        }

        [Test]
        public void Detect_CleanProject_FindsNothing()
        {
            Assert.AreEqual(Yes2SDKJestConflict.None, Detect());
        }

        [Test]
        public void Detect_PackageFolder_FlagsPackage()
        {
            Assert.AreEqual(Yes2SDKJestConflict.Package, Detect(packageFolder: true));
        }

        [Test]
        public void Detect_JestAssemblyLoaded_FlagsAssembly()
        {
            var found = Detect(assemblies: new[] { "UnityEngine", "com.jest.sdk" });
            Assert.AreEqual(Yes2SDKJestConflict.Assembly, found);
        }

        [Test]
        public void Detect_SimilarAssemblyNames_AreIgnored()
        {
            var found = Detect(assemblies: new[] { "com.jest.sdk.Editor", "Com.Jest.Sdk", "jest" });
            Assert.AreEqual(Yes2SDKJestConflict.None, found);
        }

        [Test]
        public void Detect_LeftoverInitScript_FlagsInitScript()
        {
            Assert.AreEqual(Yes2SDKJestConflict.InitScript, Detect(initScript: true));
        }

        [Test]
        public void Detect_FullInstall_FlagsEverySignal()
        {
            var found = Detect(packageFolder: true, initScript: true, assemblies: new[] { "com.jest.sdk" });
            Assert.AreEqual(
                Yes2SDKJestConflict.Package | Yes2SDKJestConflict.Assembly | Yes2SDKJestConflict.InitScript,
                found);
        }

        [Test]
        public void BuildWarning_None_ReturnsNull()
        {
            Assert.IsNull(Yes2SDKJestPackageCheck.BuildWarning(Yes2SDKJestConflict.None));
        }

        [Test]
        public void BuildWarning_Package_NamesThePackageAndTheFix()
        {
            var message = Yes2SDKJestPackageCheck.BuildWarning(Yes2SDKJestConflict.Package);
            StringAssert.StartsWith("[Yes2SDK]", message);
            StringAssert.Contains("Yes2SDK already loads the Jest SDK", message);
            StringAssert.Contains("com.jest.sdk", message);
            StringAssert.Contains("Package Manager", message);
            StringAssert.DoesNotContain("init.jspre", message);
        }

        [Test]
        public void BuildWarning_InitScriptOnly_PointsAtTheScript()
        {
            var message = Yes2SDKJestPackageCheck.BuildWarning(Yes2SDKJestConflict.InitScript);
            StringAssert.Contains("Assets/com.jest.sdk/init.jspre", message);
            StringAssert.DoesNotContain("Package Manager", message);
        }

        [Test]
        public void BuildWarning_PackageAndAssembly_ListsThePackageOnce()
        {
            var message = Yes2SDKJestPackageCheck.BuildWarning(
                Yes2SDKJestConflict.Package | Yes2SDKJestConflict.Assembly);
            var first = message.IndexOf("Package Manager", System.StringComparison.Ordinal);
            Assert.AreEqual(-1, message.IndexOf("Package Manager", first + 1, System.StringComparison.Ordinal));
        }
    }
}
