using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

        // ---- registration prompt and IsAuthenticated ----
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Auth_IsAuthenticatedJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_Auth_ShowRegistrationPromptJS(int promptId, string optionsJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Auth_RegistrationPromptLoginJS(int promptId);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Auth_RegistrationPromptCloseJS(int promptId);
        // ---- end registration prompt and IsAuthenticated ----
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

        // Task-returning overloads.

        public Task<AuthUser> GetCurrentUserAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<AuthUser>(
                (success, error) => GetCurrentUserAsync(success, error),
                cancellationToken);

        public Task<AuthUser> SignInAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<AuthUser>(
                (success, error) => SignInAsync(success, error),
                cancellationToken);

        public Task<string> GetTokenAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetTokenAsync(success, error),
                cancellationToken);

        public Task<bool> ShowAccountLinkPromptAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<bool>(
                (success, error) => ShowAccountLinkPromptAsync(success, error),
                cancellationToken);

        #endregion

        #region Registration Prompt and IsAuthenticated

        private const string RegistrationContext = "Yes2SDK.Auth.ShowRegistrationPrompt";
        private const string RegistrationCode = "{{registrationCode}}";
        private const int MaxRegistrationMessageLength = 140;

        // Open prompts by id. A prompt is removed before its onClose runs, so a
        // duplicate or late close message finds nothing and is dropped.
        private static readonly Dictionary<int, RegistrationPrompt> _openPrompts = new Dictionary<int, RegistrationPrompt>();
        private static int _nextPromptId;

        /// <summary>
        /// Whether the player is signed in (registered) on the platform.
        /// False before the SDK is initialized, on platforms without player
        /// accounts, and on any error.
        /// </summary>
        public bool IsAuthenticated()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Auth_IsAuthenticatedJS();
#elif UNITY_EDITOR
            return Yes2SDKEditorMock.IsRegisteredNow;
#else
            return false;
#endif
        }

        /// <summary>
        /// Show the platform's registration prompt for a guest player and get a
        /// handle whose <see cref="RegistrationPrompt.Login"/> and
        /// <see cref="RegistrationPrompt.Close"/> the game wires to its own
        /// buttons. Returns null and calls onError right away when the prompt
        /// cannot be shown: the player is already registered (INVALID_OPERATION),
        /// the message breaks the platform rules (INVALID_PARAM), the SDK is not
        /// initialized, or the platform has no registration prompt.
        /// </summary>
        /// <param name="options">Optional theme, data and message.</param>
        /// <param name="onClose">Called once when the prompt closes.</param>
        /// <param name="onError">Called synchronously when the prompt cannot be shown.</param>
        /// <returns>The open prompt, or null on failure.</returns>
        public RegistrationPrompt ShowRegistrationPrompt(
            RegistrationPromptOptions options = null,
            Action onClose = null,
            Action<Error> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string optionsJson;
            try
            {
                optionsJson = options != null ? options.ToJson() : "{}";
            }
            catch (Exception e)
            {
                onError?.Invoke(new Error { Code = "INVALID_PARAM", Message = $"Registration prompt options could not be serialized: {e.Message}", Context = RegistrationContext });
                return null;
            }

            // Registered before the call so a close reported during the call
            // still finds its prompt.
            var prompt = RegisterPrompt(onClose);
            string result = Yes2SDK_Auth_ShowRegistrationPromptJS(prompt.Id, optionsJson);
            return CompleteShow(prompt, result, onError);
#elif UNITY_EDITOR
            // Same order as the device path: options are serialized first.
            try
            {
                if (options != null) options.ToJson();
            }
            catch (Exception e)
            {
                string serializeMessage = $"Registration prompt options could not be serialized: {e.Message}";
                Yes2Log.Log($"Mock: Auth.ShowRegistrationPrompt() - INVALID_PARAM: {serializeMessage}");
                onError?.Invoke(new Error { Code = "INVALID_PARAM", Message = serializeMessage, Context = RegistrationContext });
                return null;
            }
            if (Yes2SDKEditorMock.IsRegisteredNow)
            {
                Yes2Log.Log("Mock: Auth.ShowRegistrationPrompt() - player is registered, INVALID_OPERATION");
                onError?.Invoke(new Error
                {
                    Code = "INVALID_OPERATION",
                    Message = "The player is already registered; the registration prompt is for guests only",
                    Context = RegistrationContext
                });
                return null;
            }
            if (options != null && options.Message != null
                && !TryValidateRegistrationMessage(options.Message, out var messageError))
            {
                Yes2Log.Log($"Mock: Auth.ShowRegistrationPrompt() - INVALID_PARAM: {messageError}");
                onError?.Invoke(new Error { Code = "INVALID_PARAM", Message = messageError, Context = RegistrationContext });
                return null;
            }
            var mockPrompt = RegisterPrompt(onClose);
            Yes2Log.Log($"Mock: Auth.ShowRegistrationPrompt() - prompt {mockPrompt.Id} open; Login() completes a mock registration, Close() closes it");
            return mockPrompt;
#else
            onError?.Invoke(FeatureNotSupportedError(RegistrationContext));
            return null;
#endif
        }

        internal static void LoginRegistrationPrompt(RegistrationPrompt prompt)
        {
            if (!prompt.IsOpen)
            {
                Yes2Log.Warning($"Auth: registration prompt {prompt.Id} is already closed; Login() ignored");
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Auth_RegistrationPromptLoginJS(prompt.Id);
#elif UNITY_EDITOR
            Yes2SDKEditorMock.SessionRegisteredOverride = true;
            Yes2Log.Log($"Mock: registration prompt {prompt.Id} - mock registration complete");
            prompt.IsOpen = false;
            DeliverMockClose(prompt.Id);
#endif
        }

        internal static void CloseRegistrationPrompt(RegistrationPrompt prompt)
        {
            if (!prompt.IsOpen)
            {
                Yes2Log.Warning($"Auth: registration prompt {prompt.Id} is already closed; Close() ignored");
                return;
            }
            prompt.IsOpen = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Auth_RegistrationPromptCloseJS(prompt.Id);
#elif UNITY_EDITOR
            Yes2Log.Log($"Mock: registration prompt {prompt.Id} closed");
            DeliverMockClose(prompt.Id);
#endif
        }

        /// <summary>
        /// Bridge entry for "OnRegistrationPromptClose" (payload: the prompt id).
        /// Removes the prompt before running its onClose; unknown or duplicate
        /// ids are dropped with a warning.
        /// </summary>
        internal static void HandleRegistrationPromptClose(string idText)
        {
            if (!int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
                || !_openPrompts.TryGetValue(id, out var prompt))
            {
                Yes2Log.Warning($"Auth: dropped registration prompt close for '{idText}': no such prompt open (already closed or unknown)");
                return;
            }

            _openPrompts.Remove(id);
            prompt.IsOpen = false;
            prompt.OnClose?.Invoke();
        }

        // Whitespace set of JavaScript String.prototype.trim (includes U+FEFF,
        // excludes U+0085 and U+180E), which string.Trim() does not match.
        private static bool IsJsWhitespace(char c)
        {
            return c == '\t' || c == '\n' || c == '\v' || c == '\f' || c == '\r' || c == ' '
                || c == '\u00A0' || c == '\u1680' || (c >= '\u2000' && c <= '\u200A')
                || c == '\u2028' || c == '\u2029' || c == '\u202F' || c == '\u205F'
                || c == '\u3000' || c == '\uFEFF';
        }

        private static bool IsBlankLikeJs(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!IsJsWhitespace(text[i])) return false;
            }
            return true;
        }

        /// <summary>
        /// Platform rules for a custom registration message, checked by the
        /// Editor mock with the same messages the platform reports.
        /// </summary>
        internal static bool TryValidateRegistrationMessage(string message, out string error)
        {
            error = null;
            if (message == null || IsBlankLikeJs(message))
            {
                error = "Registration message must not be empty or whitespace only";
                return false;
            }
            if (message.Length > MaxRegistrationMessageLength)
            {
                error = $"Registration message must be at most {MaxRegistrationMessageLength} characters";
                return false;
            }
            string[] parts = message.Split(new[] { RegistrationCode }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                error = $"Registration message must contain {RegistrationCode} exactly once";
                return false;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(parts[0] + parts[1], @"\{\{[^}]*\}\}"))
            {
                error = $"Registration message must not contain placeholders other than {RegistrationCode}";
                return false;
            }
            string before = parts[0];
            string after = parts[1];
            int prevIndex = before.Length - 1;
            if (prevIndex > 0 && char.IsSurrogatePair(before[prevIndex - 1], before[prevIndex]))
            {
                prevIndex--;
            }
            bool prevJoins = prevIndex >= 0 && IsWordLike(before, prevIndex);
            bool nextJoins = after.Length > 0 && IsWordLike(after, 0);
            if (prevJoins || nextJoins)
            {
                error = $"{RegistrationCode} must not run into a neighbouring letter, digit, combining mark or underscore; separate it with a space or punctuation";
                return false;
            }
            return true;
        }

        // Letter, digit, combining mark or underscore at index (surrogate pairs read as one character).
        private static bool IsWordLike(string text, int index)
        {
            if (text[index] == '_') return true;
            switch (CharUnicodeInfo.GetUnicodeCategory(text, index))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                    return true;
                default:
                    return false;
            }
        }

        private static RegistrationPrompt RegisterPrompt(Action onClose)
        {
            // Ids only need to be unique among open prompts, so wrap back to 1
            // rather than go negative after int.MaxValue prompts.
            _nextPromptId = _nextPromptId == int.MaxValue ? 1 : _nextPromptId + 1;
            var prompt = new RegistrationPrompt(_nextPromptId, onClose);
            _openPrompts[prompt.Id] = prompt;
            return prompt;
        }

        // jsResult is "" on success, else error JSON {code, message, context}.
        private static RegistrationPrompt CompleteShow(RegistrationPrompt prompt, string jsResult, Action<Error> onError)
        {
            if (string.IsNullOrEmpty(jsResult))
            {
                return prompt;
            }

            _openPrompts.Remove(prompt.Id);
            prompt.IsOpen = false;
            onError?.Invoke(Bridge.ParseError(jsResult));
            return null;
        }

#if UNITY_EDITOR
        // The device path reports the close through Bridge, which logs a
        // throwing onClose instead of letting it escape; the mock does the same.
        private static void DeliverMockClose(int id)
        {
            try
            {
                HandleRegistrationPromptClose(id.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Yes2Log.Error($"Bridge: callback 'OnRegistrationPromptClose' threw: {e}");
            }
        }

        // Domain reload may be disabled, so open prompts from the last play are
        // cleared on each play.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistrationPromptEditorState()
        {
            _openPrompts.Clear();
        }
#endif

        internal static RegistrationPrompt CompleteShowForTests(string jsResult, Action onClose, Action<Error> onError)
        {
            return CompleteShow(RegisterPrompt(onClose), jsResult, onError);
        }

        internal static int OpenPromptCountForTests()
        {
            return _openPrompts.Count;
        }

        internal static void ResetRegistrationPromptStateForTests()
        {
            _openPrompts.Clear();
            _nextPromptId = 0;
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
