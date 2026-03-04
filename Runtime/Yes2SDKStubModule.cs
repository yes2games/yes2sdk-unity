using System;

namespace Yes2SDK
{
    /// <summary>
    /// Base class for stub modules that return FeatureNotSupported on all platforms.
    /// </summary>
    public abstract class Yes2SDKStubModule
    {
        protected abstract string FeatureName { get; }
        protected abstract string ModuleName { get; }

        public bool IsSupported() => false;

        protected void Stub(Action<Error> onError, string method, string args = "")
        {
            var prefix = IsEditor() ? "Mock" : "Stub";
            Yes2Log.Log($"{prefix}: {method}({args}) — FeatureNotSupported");
            onError?.Invoke(new Error
            {
                Code = "FeatureNotSupported",
                Message = $"{FeatureName} not supported on the current platform",
                Context = $"Yes2SDK.{ModuleName}.{method}"
            });
        }

        protected void StubLog(string method, string args = "")
        {
            var prefix = IsEditor() ? "Mock" : "Stub";
            Yes2Log.Log($"{prefix}: {method}({args}) — FeatureNotSupported");
        }

        private static bool IsEditor()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
}
