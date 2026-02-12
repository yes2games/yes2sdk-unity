using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Auth API for Yes2SDK.
    /// Provides user authentication. Fully supported on CrazyGames; stubs on Poki.
    /// </summary>
    public class Yes2SDKAuth
    {
        #region Static Callback Fields

        private static Action<AuthUser> _getCurrentUserSuccessCallback;
        private static Action<Error> _getCurrentUserErrorCallback;
        private static Action<AuthUser> _signInSuccessCallback;
        private static Action<Error> _signInErrorCallback;
        private static Action<string> _getTokenSuccessCallback;
        private static Action<Error> _getTokenErrorCallback;
        private static Action<bool> _accountLinkSuccessCallback;
        private static Action<Error> _accountLinkErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Auth_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Auth_GetCurrentUserAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Auth_SignInAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Auth_GetTokenAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Auth_ShowAccountLinkPromptAsyncJS();
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether authentication is supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Auth_IsSupportedJS();
#else
            Yes2Log.Log("Mock: Auth.IsSupported() — returning false");
            return false;
#endif
        }

        /// <summary>
        /// Get the currently authenticated user, or null if not authenticated.
        /// </summary>
        public void GetCurrentUserAsync(Action<AuthUser> onSuccess = null, Action<Error> onError = null)
        {
            _getCurrentUserSuccessCallback = onSuccess;
            _getCurrentUserErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Auth_GetCurrentUserAsyncJS();
#else
            Yes2Log.Log("Mock: Auth.GetCurrentUserAsync() — returning null user");
            InvokeGetCurrentUserSuccess("null");
#endif
        }

        /// <summary>
        /// Show the sign-in prompt and return the authenticated user.
        /// </summary>
        public void SignInAsync(Action<AuthUser> onSuccess = null, Action<Error> onError = null)
        {
            _signInSuccessCallback = onSuccess;
            _signInErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Auth_SignInAsyncJS();
#else
            Yes2Log.Log("Mock: Auth.SignInAsync() — FeatureNotSupported");
            InvokeSignInError(FeatureNotSupportedError("Yes2SDK.Auth.SignInAsync"));
#endif
        }

        /// <summary>
        /// Get the current user's authentication token (JWT).
        /// </summary>
        public void GetTokenAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getTokenSuccessCallback = onSuccess;
            _getTokenErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Auth_GetTokenAsyncJS();
#else
            Yes2Log.Log("Mock: Auth.GetTokenAsync() — FeatureNotSupported");
            InvokeGetTokenError(FeatureNotSupportedError("Yes2SDK.Auth.GetTokenAsync"));
#endif
        }

        /// <summary>
        /// Show a prompt to link the user's account.
        /// </summary>
        public void ShowAccountLinkPromptAsync(Action<bool> onSuccess = null, Action<Error> onError = null)
        {
            _accountLinkSuccessCallback = onSuccess;
            _accountLinkErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Auth_ShowAccountLinkPromptAsyncJS();
#else
            Yes2Log.Log("Mock: Auth.ShowAccountLinkPromptAsync() — FeatureNotSupported");
            InvokeAccountLinkError(FeatureNotSupportedError("Yes2SDK.Auth.ShowAccountLinkPromptAsync"));
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeGetCurrentUserSuccess(string userJson)
        {
            if (_getCurrentUserSuccessCallback != null)
            {
                try
                {
                    if (string.IsNullOrEmpty(userJson) || userJson == "null")
                    {
                        _getCurrentUserSuccessCallback.Invoke(default);
                    }
                    else
                    {
                        var user = JsonConvert.DeserializeObject<AuthUser>(userJson);
                        _getCurrentUserSuccessCallback.Invoke(user);
                    }
                }
                catch (Exception e)
                {
                    Yes2Log.Error($"Failed to parse auth user JSON: {e.Message}");
                    _getCurrentUserErrorCallback?.Invoke(new Error
                    {
                        Code = "Unknown",
                        Message = $"Failed to parse auth user: {e.Message}",
                        Context = "Yes2SDK.Auth.GetCurrentUserAsync"
                    });
                }
            }
            _getCurrentUserSuccessCallback = null;
            _getCurrentUserErrorCallback = null;
        }

        internal static void InvokeGetCurrentUserError(Error error)
        {
            _getCurrentUserErrorCallback?.Invoke(error);
            _getCurrentUserSuccessCallback = null;
            _getCurrentUserErrorCallback = null;
        }

        internal static void InvokeSignInSuccess(string userJson)
        {
            if (_signInSuccessCallback != null)
            {
                try
                {
                    var user = JsonConvert.DeserializeObject<AuthUser>(userJson);
                    _signInSuccessCallback.Invoke(user);
                }
                catch (Exception e)
                {
                    Yes2Log.Error($"Failed to parse sign-in user JSON: {e.Message}");
                    _signInErrorCallback?.Invoke(new Error
                    {
                        Code = "Unknown",
                        Message = $"Failed to parse sign-in user: {e.Message}",
                        Context = "Yes2SDK.Auth.SignInAsync"
                    });
                }
            }
            _signInSuccessCallback = null;
            _signInErrorCallback = null;
        }

        internal static void InvokeSignInError(Error error)
        {
            _signInErrorCallback?.Invoke(error);
            _signInSuccessCallback = null;
            _signInErrorCallback = null;
        }

        internal static void InvokeGetTokenSuccess(string token)
        {
            _getTokenSuccessCallback?.Invoke(token);
            _getTokenSuccessCallback = null;
            _getTokenErrorCallback = null;
        }

        internal static void InvokeGetTokenError(Error error)
        {
            _getTokenErrorCallback?.Invoke(error);
            _getTokenSuccessCallback = null;
            _getTokenErrorCallback = null;
        }

        internal static void InvokeAccountLinkSuccess(string resultStr)
        {
            _accountLinkSuccessCallback?.Invoke(resultStr == "true" || resultStr == "1");
            _accountLinkSuccessCallback = null;
            _accountLinkErrorCallback = null;
        }

        internal static void InvokeAccountLinkError(Error error)
        {
            _accountLinkErrorCallback?.Invoke(error);
            _accountLinkSuccessCallback = null;
            _accountLinkErrorCallback = null;
        }

        #endregion

        #region Private Helpers

        private static Error FeatureNotSupportedError(string context)
        {
            return new Error
            {
                Code = "FeatureNotSupported",
                Message = "This feature is not supported on the current platform",
                Context = context
            };
        }

        #endregion
    }
}
