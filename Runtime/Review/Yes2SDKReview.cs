using System;
using System.Runtime.InteropServices;

namespace Yes2SDK
{
    /// <summary>
    /// Game rating / feedback-prompt API for Yes2SDK.
    /// Backed by the platform review API via the Core SDK (window.Yes2SDK.review).
    /// The prompt is gated behind an eligibility check. Result payloads are
    /// delivered to onSuccess as JSON strings. Platforms without a rating service
    /// report ineligibility and a no-op feedback result.
    /// </summary>
    public class Yes2SDKReview
    {
        #region Static Callback Fields

        private static Action<string> _canReviewSuccessCallback;
        private static Action<Error> _canReviewErrorCallback;
        private static Action<string> _requestReviewSuccessCallback;
        private static Action<Error> _requestReviewErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Review_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Review_CanReviewAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Review_RequestReviewAsyncJS();
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether the rating prompt is supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Review_IsSupportedJS();
#else
            Yes2Log.Log("Mock: Review.IsSupported() — returning false");
            return false;
#endif
        }

        /// <summary>
        /// Check whether the player can currently be shown the rating prompt.
        /// onSuccess receives a JSON object { canReview, reason? }.
        /// </summary>
        public void CanReviewAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _canReviewSuccessCallback = onSuccess;
            _canReviewErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Review_CanReviewAsyncJS();
#else
            Yes2Log.Log("Mock: Review.CanReviewAsync() — returning canReview=false");
            InvokeCanReviewSuccess("{\"canReview\":false}");
#endif
        }

        /// <summary>
        /// Request the in-game rating / feedback prompt. onSuccess receives a JSON
        /// object { feedbackSent }.
        /// </summary>
        public void RequestReviewAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _requestReviewSuccessCallback = onSuccess;
            _requestReviewErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Review_RequestReviewAsyncJS();
#else
            Yes2Log.Log("Mock: Review.RequestReviewAsync() — returning feedbackSent=false");
            InvokeRequestReviewSuccess("{\"feedbackSent\":false}");
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeCanReviewSuccess(string eligibilityJson)
        {
            _canReviewSuccessCallback?.Invoke(eligibilityJson);
            _canReviewSuccessCallback = null;
            _canReviewErrorCallback = null;
        }

        internal static void InvokeCanReviewError(Error error)
        {
            _canReviewErrorCallback?.Invoke(error);
            _canReviewSuccessCallback = null;
            _canReviewErrorCallback = null;
        }

        internal static void InvokeRequestReviewSuccess(string resultJson)
        {
            _requestReviewSuccessCallback?.Invoke(resultJson);
            _requestReviewSuccessCallback = null;
            _requestReviewErrorCallback = null;
        }

        internal static void InvokeRequestReviewError(Error error)
        {
            _requestReviewErrorCallback?.Invoke(error);
            _requestReviewSuccessCallback = null;
            _requestReviewErrorCallback = null;
        }

        #endregion
    }
}
