using System;

namespace Yes2SDK
{
    /// <summary>
    /// Context API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKContext : Yes2SDKStubModule
    {
        protected override string FeatureName => "Context";
        protected override string ModuleName => "Context";

        public string GetContext()
        {
            StubLog(nameof(GetContext));
            return null;
        }

        public void SwitchAsync(string contextId, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(SwitchAsync), contextId);

        public void ChooseAsync(Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(ChooseAsync));

        public void CreateAsync(string playerId, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(CreateAsync), playerId);

        public void ShareAsync(string text, string imageBase64, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(ShareAsync));
    }
}
