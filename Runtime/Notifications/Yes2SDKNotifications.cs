using System;

namespace Yes2SDK
{
    /// <summary>
    /// Notifications API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKNotifications : Yes2SDKStubModule
    {
        protected override string FeatureName => "Notifications";
        protected override string ModuleName => "Notifications";

        public void ScheduleAsync(string title, string body, int delaySec, string dataJson, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(ScheduleAsync), title);

        public void CancelAsync(string notificationId, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(CancelAsync), notificationId);

        public void CancelAllAsync(Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(CancelAllAsync));
    }
}
