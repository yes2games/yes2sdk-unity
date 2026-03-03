using System;

namespace Yes2SDK
{
    /// <summary>
    /// Leaderboard API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKLeaderboard : Yes2SDKStubModule
    {
        protected override string FeatureName => "Leaderboards";
        protected override string ModuleName => "Leaderboard";

        public void GetLeaderboardAsync(string leaderboardId, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetLeaderboardAsync), leaderboardId);

        public void SetScoreAsync(string leaderboardId, int score, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(SetScoreAsync), $"{leaderboardId}, {score}");

        public void GetEntriesAsync(string leaderboardId, int count, int offset, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetEntriesAsync), $"{leaderboardId}, {count}, {offset}");

        public void GetPlayerEntryAsync(string leaderboardId, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetPlayerEntryAsync), leaderboardId);
    }
}
