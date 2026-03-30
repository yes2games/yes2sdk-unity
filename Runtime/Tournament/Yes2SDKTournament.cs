using System;

namespace Yes2SDK
{
    /// <summary>
    /// Tournament API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKTournament : Yes2SDKStubModule
    {
        protected override string FeatureName => "Tournaments";
        protected override string ModuleName => "Tournament";

        public void GetCurrentAsync(Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetCurrentAsync));

        public void GetAllAsync(Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetAllAsync));

        public void CreateAsync(string configJson, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(CreateAsync));

        public void PostScoreAsync(int score, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(PostScoreAsync), score.ToString());

        public void JoinAsync(string tournamentId, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(JoinAsync), tournamentId);
    }
}
