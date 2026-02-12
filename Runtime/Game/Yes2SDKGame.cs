using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Game API for Yes2SDK.
    /// Provides gameplay lifecycle, invite links, settings, and clipboard.
    /// Fully supported on CrazyGames; partial on Poki (gameplayStart/Stop only).
    /// </summary>
    public class Yes2SDKGame
    {
        #region Static Callback Fields

        private static Action<string> _inviteLinkSuccessCallback;
        private static Action<Error> _inviteLinkErrorCallback;

        #endregion

        #region Events

        /// <summary>
        /// Called when platform game settings change (e.g., muteAudio, disableChat).
        /// </summary>
        public static event Action<GameSettings> OnSettingsChanged;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_Game_GameplayStartJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Game_GameplayStopJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Game_HappyTimeJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Game_InviteLinkAsyncJS(string paramsJson);

        [DllImport("__Internal")]
        private static extern string Yes2SDK_Game_GetInviteParamJS(string key);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Game_ShowInviteButtonJS(string paramsJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Game_HideInviteButtonJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_Game_GetSettingsJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Game_CopyToClipboardJS(string text);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Notify the platform that gameplay has started.
        /// </summary>
        public void GameplayStart()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Game_GameplayStartJS();
#else
            Yes2Log.Log("Mock: Game.GameplayStart()");
#endif
        }

        /// <summary>
        /// Notify the platform that gameplay has stopped.
        /// </summary>
        public void GameplayStop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Game_GameplayStopJS();
#else
            Yes2Log.Log("Mock: Game.GameplayStop()");
#endif
        }

        /// <summary>
        /// Signal to the platform that the player is having a good time (CrazyGames happytime).
        /// </summary>
        public void HappyTime()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Game_HappyTimeJS();
#else
            Yes2Log.Log("Mock: Game.HappyTime()");
#endif
        }

        /// <summary>
        /// Generate an invite link with the specified parameters.
        /// </summary>
        public void InviteLinkAsync(Dictionary<string, string> parameters, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _inviteLinkSuccessCallback = onSuccess;
            _inviteLinkErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            var paramsJson = JsonConvert.SerializeObject(parameters ?? new Dictionary<string, string>());
            Yes2SDK_Game_InviteLinkAsyncJS(paramsJson);
#else
            Yes2Log.Log("Mock: Game.InviteLinkAsync() — FeatureNotSupported");
            InvokeInviteLinkError(FeatureNotSupportedError("Yes2SDK.Game.InviteLinkAsync"));
#endif
        }

        /// <summary>
        /// Get the value of an invite parameter from the URL.
        /// </summary>
        public string GetInviteParam(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Game_GetInviteParamJS(key);
#else
            Yes2Log.Log($"Mock: Game.GetInviteParam({key}) — returning \"\"");
            return "";
#endif
        }

        /// <summary>
        /// Show the invite button overlay with the specified parameters.
        /// </summary>
        public void ShowInviteButton(Dictionary<string, string> parameters = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var paramsJson = JsonConvert.SerializeObject(parameters ?? new Dictionary<string, string>());
            Yes2SDK_Game_ShowInviteButtonJS(paramsJson);
#else
            Yes2Log.Log("Mock: Game.ShowInviteButton()");
#endif
        }

        /// <summary>
        /// Hide the invite button overlay.
        /// </summary>
        public void HideInviteButton()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Game_HideInviteButtonJS();
#else
            Yes2Log.Log("Mock: Game.HideInviteButton()");
#endif
        }

        /// <summary>
        /// Get the current platform game settings (muteAudio, disableChat).
        /// </summary>
        public GameSettings GetSettings()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var json = Yes2SDK_Game_GetSettingsJS();
            try
            {
                return JsonConvert.DeserializeObject<GameSettings>(json);
            }
            catch
            {
                return default;
            }
#else
            Yes2Log.Log("Mock: Game.GetSettings()");
            return default;
#endif
        }

        /// <summary>
        /// Copy text to the clipboard (platform-assisted on CrazyGames).
        /// </summary>
        public void CopyToClipboard(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Game_CopyToClipboardJS(text);
#else
            Yes2Log.Log($"Mock: Game.CopyToClipboard({text})");
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeInviteLinkSuccess(string link)
        {
            _inviteLinkSuccessCallback?.Invoke(link);
            _inviteLinkSuccessCallback = null;
            _inviteLinkErrorCallback = null;
        }

        internal static void InvokeInviteLinkError(Error error)
        {
            _inviteLinkErrorCallback?.Invoke(error);
            _inviteLinkSuccessCallback = null;
            _inviteLinkErrorCallback = null;
        }

        internal static void InvokeSettingsChanged(string settingsJson)
        {
            try
            {
                var settings = JsonConvert.DeserializeObject<GameSettings>(settingsJson);
                OnSettingsChanged?.Invoke(settings);
            }
            catch (Exception e)
            {
                Yes2Log.Error($"Failed to parse game settings: {e.Message}");
            }
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
