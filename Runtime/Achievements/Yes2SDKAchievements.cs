using System;

namespace Yes2SDK
{
    /// <summary>
    /// Achievements API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKAchievements : Yes2SDKStubModule
    {
        protected override string FeatureName => "Achievements";
        protected override string ModuleName => "Achievements";

        public void GetAchievementsAsync(Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetAchievementsAsync));

        public void UnlockAsync(string achievementId, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(UnlockAsync), achievementId);

        public void SetProgressAsync(string achievementId, int progress, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(SetProgressAsync), $"{achievementId}, {progress}");
    }
}
