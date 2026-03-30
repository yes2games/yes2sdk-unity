using System;

namespace Yes2SDK
{
    /// <summary>
    /// Stats API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKStats : Yes2SDKStubModule
    {
        protected override string FeatureName => "Stats";
        protected override string ModuleName => "Stats";

        public void GetStatsAsync(string[] keys, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetStatsAsync));

        public void SetStatsAsync(string statsJson, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(SetStatsAsync));

        public void IncrementStatsAsync(string incrementsJson, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(IncrementStatsAsync));
    }
}
