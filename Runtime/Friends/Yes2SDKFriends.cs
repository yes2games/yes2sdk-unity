using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Friends API for Yes2SDK.
    /// Provides paginated friends list. Fully supported on CrazyGames; stubs on Poki.
    /// </summary>
    public class Yes2SDKFriends
    {
        #region Static Callback Fields

        private static Action<FriendsPage> _listFriendsSuccessCallback;
        private static Action<Error> _listFriendsErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_Friends_ListFriendsAsyncJS(int page, int size);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// List the current user's friends with pagination.
        /// </summary>
        /// <param name="page">Page index (0-based).</param>
        /// <param name="size">Number of friends per page.</param>
        /// <param name="onSuccess">Called with the paginated friends result.</param>
        /// <param name="onError">Called if the request fails.</param>
        public void ListFriendsAsync(int page, int size, Action<FriendsPage> onSuccess = null, Action<Error> onError = null)
        {
            _listFriendsSuccessCallback = onSuccess;
            _listFriendsErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Friends_ListFriendsAsyncJS(page, size);
#else
            Yes2Log.Log("Mock: Friends.ListFriendsAsync() — FeatureNotSupported");
            InvokeListFriendsError(FeatureNotSupportedError("Yes2SDK.Friends.ListFriendsAsync"));
#endif
        }

        public Task<FriendsPage> ListFriendsAsync(int page, int size, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<FriendsPage>(
                (success, error) => ListFriendsAsync(page, size, success, error),
                cancellationToken);

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeListFriendsSuccess(string pageJson)
        {
            if (_listFriendsSuccessCallback != null)
            {
                try
                {
                    var page = JsonConvert.DeserializeObject<FriendsPage>(pageJson);
                    _listFriendsSuccessCallback.Invoke(page);
                }
                catch (Exception e)
                {
                    Yes2Log.Error($"Failed to parse friends page JSON: {e.Message}");
                    _listFriendsErrorCallback?.Invoke(new Error
                    {
                        Code = "Unknown",
                        Message = $"Failed to parse friends data: {e.Message}",
                        Context = "Yes2SDK.Friends.ListFriendsAsync"
                    });
                }
            }
            _listFriendsSuccessCallback = null;
            _listFriendsErrorCallback = null;
        }

        internal static void InvokeListFriendsError(Error error)
        {
            _listFriendsErrorCallback?.Invoke(error);
            _listFriendsSuccessCallback = null;
            _listFriendsErrorCallback = null;
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
