using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Referrals API: share a referral link and list the players who joined through it.
    /// Data attached to a share reaches the invited player through
    /// <c>Yes2SDK.Session.GetEntryPointData()</c>.
    /// Reports FeatureNotSupported on platforms without referrals; check <see cref="IsSupported"/>.
    /// </summary>
    public class Yes2SDKReferrals
    {
        #region Request Tracking

        /// <summary>The referrals operation a request belongs to.</summary>
        internal enum Operation
        {
            Share,
            List
        }

        private sealed class PendingRequest
        {
            public Operation Operation;
            public Action<string> OnSuccess;
            public Action<Error> OnError;
        }

        // Every call gets its own id, carried through the JS bridge and echoed
        // back on its response, so a late response to an abandoned call cannot
        // complete the retry that replaced it.
        private static readonly Dictionary<int, PendingRequest> _pending = new Dictionary<int, PendingRequest>();
        private static int _nextRequestId;

        // Separates the request id from the payload in a bridge message.
        private const char EnvelopeSeparator = '|';

        private const string ShareContext = "Yes2SDK.Referrals.ShareAsync";
        private const string ListContext = "Yes2SDK.Referrals.ListAsync";

        private static readonly JsonSerializerSettings ParseSettings = new JsonSerializerSettings
        {
            // Keep ISO timestamps exactly as the platform sent them.
            DateParseHandling = DateParseHandling.None
        };

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Referrals_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Referrals_ShareAsyncJS(int requestId, string optionsJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Referrals_ListAsyncJS(int requestId);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether referrals are supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Referrals_IsSupportedJS();
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                Yes2Log.Log("Mock: Referrals.IsSupported() - returning true (referrals mock enabled)");
                return true;
            }
#endif
            Yes2Log.Log("Mock: Referrals.IsSupported() - returning false");
            return false;
#endif
        }

        /// <summary>
        /// Open the platform share dialog with a referral link.
        /// onSuccess receives <see cref="ReferralShareResult.Canceled"/> = true when the player closed
        /// the dialog without sharing. Null options or an empty <see cref="ReferralShareOptions.Reference"/>
        /// call onError synchronously with InvalidParams.
        /// </summary>
        public void ShareAsync(
            ReferralShareOptions options,
            Action<ReferralShareResult> onSuccess = null,
            Action<Error> onError = null)
        {
            if (options == null || string.IsNullOrWhiteSpace(options.Reference))
            {
                Yes2Log.Warning("Referrals.ShareAsync: options.Reference is required");
                onError?.Invoke(new Error
                {
                    Code = "InvalidParams",
                    Message = options == null
                        ? "options must not be null"
                        : "options.Reference must be a non-empty string",
                    Context = ShareContext
                });
                return;
            }

            int requestId = Register(Operation.Share, TypedShare(onSuccess, onError), onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Referrals_ShareAsyncJS(requestId, options.ToJson());
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                MockShare(requestId, options, Yes2SDKEditorMock.ReferralShareResult);
                return;
            }
#endif
            Yes2Log.Log($"Mock: Referrals.ShareAsync('{options.Reference}') - FeatureNotSupported");
            CompleteError(Operation.Share, requestId, FeatureNotSupportedError(ShareContext));
#endif
        }

        /// <summary>
        /// List the current player's referral conversions, grouped by reference.
        /// Verify <see cref="ReferralList.SignedRequest"/> on your server before granting rewards.
        /// </summary>
        public void ListAsync(Action<ReferralList> onSuccess = null, Action<Error> onError = null)
        {
            int requestId = Register(Operation.List, TypedList(onSuccess, onError), onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Referrals_ListAsyncJS(requestId);
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                MockList(requestId, Yes2SDKEditorMock.ReferralConversions);
                return;
            }
#endif
            Yes2Log.Log("Mock: Referrals.ListAsync() - FeatureNotSupported");
            CompleteError(Operation.List, requestId, FeatureNotSupportedError(ListContext));
#endif
        }

        // Task-returning overloads.

        public Task<ReferralShareResult> ShareAsync(ReferralShareOptions options, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<ReferralShareResult>(
                (success, error) => ShareAsync(options, success, error),
                cancellationToken);

        public Task<ReferralList> ListAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<ReferralList>(
                (success, error) => ListAsync(success, error),
                cancellationToken);

        #endregion

        #region Internal Completion (called by Bridge and the Editor mock)

        /// <summary>
        /// Bridge entry for a success message. The message is
        /// "&lt;requestId&gt;|&lt;payload&gt;", as written by Yes2SDKReferrals.jslib.
        /// </summary>
        internal static void HandleSuccessMessage(Operation operation, string message)
        {
            if (!TryParseEnvelope(operation, message, out int requestId, out string payload)) return;
            CompleteSuccess(operation, requestId, payload);
        }

        /// <summary>
        /// Bridge entry for an error message: "&lt;requestId&gt;|&lt;error JSON&gt;".
        /// </summary>
        internal static void HandleErrorMessage(Operation operation, string message, Func<string, Error> parseError)
        {
            if (!TryParseEnvelope(operation, message, out int requestId, out string payload)) return;
            CompleteError(operation, requestId, parseError(payload));
        }

        internal static void CompleteSuccess(Operation operation, int requestId, string payload)
        {
            // Take the request out BEFORE running game code, so a callback that
            // starts the next operation, or throws, cannot touch the next
            // request's callbacks. A duplicate or stale response finds nothing
            // and is dropped.
            if (!TryTake(operation, requestId, out PendingRequest request)) return;
            request.OnSuccess?.Invoke(payload);
        }

        internal static void CompleteError(Operation operation, int requestId, Error error)
        {
            if (!TryTake(operation, requestId, out PendingRequest request)) return;
            request.OnError?.Invoke(error);
        }

        #endregion

        #region Test Seams

        /// <summary>
        /// Registers a request without sending it, so tests can deliver its
        /// response later and in any order, as a delayed platform would.
        /// </summary>
        internal static int RegisterForTests(Operation operation, Action<string> onSuccess, Action<Error> onError)
        {
            return Register(operation, onSuccess, onError);
        }

        /// <summary>Registers a share request with the same typed parsing the public API uses.</summary>
        internal static int RegisterShareForTests(Action<ReferralShareResult> onSuccess, Action<Error> onError)
        {
            return Register(Operation.Share, TypedShare(onSuccess, onError), onError);
        }

        /// <summary>Registers a list request with the same typed parsing the public API uses.</summary>
        internal static int RegisterListForTests(Action<ReferralList> onSuccess, Action<Error> onError)
        {
            return Register(Operation.List, TypedList(onSuccess, onError), onError);
        }

        /// <summary>Requests still waiting for a response.</summary>
        internal static int PendingCountForTests => _pending.Count;

        /// <summary>Drops every pending request (and the mock's shared references) so tests start clean.</summary>
        internal static void ResetReferralsStateForTests()
        {
            _pending.Clear();
#if UNITY_EDITOR
            _mockSharedReferences.Clear();
#endif
        }

        /// <summary>Builds the message the jslib sends for a response.</summary>
        internal static string EnvelopeForTests(int requestId, string payload)
        {
            return requestId.ToString(CultureInfo.InvariantCulture) + EnvelopeSeparator + payload;
        }

#if UNITY_EDITOR
        /// <summary>Runs the Editor mock share with the given outcome (no Play Mode needed).</summary>
        internal static void MockShareForTests(
            ReferralShareOptions options,
            Yes2SDKEditorMock.ShareOutcome outcome,
            Action<ReferralShareResult> onSuccess,
            Action<Error> onError)
        {
            int requestId = Register(Operation.Share, TypedShare(onSuccess, onError), onError);
            MockShare(requestId, options, outcome);
        }

        /// <summary>Runs the Editor mock list with the given conversions per reference.</summary>
        internal static void MockListForTests(int conversionsPerReference, Action<ReferralList> onSuccess, Action<Error> onError)
        {
            int requestId = Register(Operation.List, TypedList(onSuccess, onError), onError);
            MockList(requestId, conversionsPerReference);
        }
#endif

        #endregion

        #region Editor Mock

#if UNITY_EDITOR
        // References shared during this Play Mode session, in share order.
        private static readonly List<string> _mockSharedReferences = new List<string>();

        private static bool MockActive =>
            Yes2SDKEditorMock.PlatformServicesEnabled && Yes2SDKEditorMock.CanShowPopups;

        private static void MockShare(int requestId, ReferralShareOptions options, Yes2SDKEditorMock.ShareOutcome outcome)
        {
            switch (outcome)
            {
                case Yes2SDKEditorMock.ShareOutcome.Cancelled:
                    Yes2Log.Log($"Mock: Referrals.ShareAsync('{options.Reference}') - player cancelled");
                    CompleteSuccess(Operation.Share, requestId, "{\"canceled\":true}");
                    return;
                case Yes2SDKEditorMock.ShareOutcome.Error:
                    Yes2Log.Log($"Mock: Referrals.ShareAsync('{options.Reference}') - simulated failure");
                    CompleteError(Operation.Share, requestId, new Error
                    {
                        Code = "PlatformError",
                        Message = "Simulated referral share failure (mock)",
                        Context = ShareContext
                    });
                    return;
                default:
                    if (!_mockSharedReferences.Contains(options.Reference))
                    {
                        _mockSharedReferences.Add(options.Reference);
                    }
                    Yes2Log.Log($"Mock: Referrals.ShareAsync('{options.Reference}') - shared");
                    CompleteSuccess(Operation.Share, requestId, "{\"canceled\":false}");
                    return;
            }
        }

        private static void MockList(int requestId, int conversionsPerReference)
        {
            string joinedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            var list = new ReferralList { SignedRequest = "mock-signed-request" };
            if (conversionsPerReference > 0)
            {
                foreach (string reference in _mockSharedReferences)
                {
                    var conversions = new List<ReferralConversion>();
                    for (int i = 1; i <= conversionsPerReference; i++)
                    {
                        conversions.Add(new ReferralConversion { PlayerId = "mock-player-" + i, JoinedAt = joinedAt });
                    }
                    list.Referrals[reference] = conversions;
                }
            }
            Yes2Log.Log($"Mock: Referrals.ListAsync() - {list.Referrals.Count} reference(s)");
            CompleteSuccess(Operation.List, requestId, JsonConvert.SerializeObject(list));
        }

        // Pending requests and mock shares can survive a stopped Play Mode
        // when Domain Reload is disabled, so clear them on each play.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorState()
        {
            _pending.Clear();
            _mockSharedReferences.Clear();
        }
#endif

        #endregion

        #region Private Helpers

        private static Action<string> TypedShare(Action<ReferralShareResult> onSuccess, Action<Error> onError)
        {
            return payload =>
            {
                if (TryDeserialize(payload, out ReferralShareResult result))
                {
                    onSuccess?.Invoke(result);
                }
                else
                {
                    onError?.Invoke(UnreadableResultError(ShareContext, payload));
                }
            };
        }

        private static Action<string> TypedList(Action<ReferralList> onSuccess, Action<Error> onError)
        {
            return payload =>
            {
                if (TryDeserialize(payload, out ReferralList list))
                {
                    if (list.Referrals == null) list.Referrals = new Dictionary<string, List<ReferralConversion>>();
                    onSuccess?.Invoke(list);
                }
                else
                {
                    onError?.Invoke(UnreadableResultError(ListContext, payload));
                }
            };
        }

        private static bool TryDeserialize<T>(string payload, out T value) where T : class
        {
            value = null;
            if (string.IsNullOrEmpty(payload)) return false;
            try
            {
                value = JsonConvert.DeserializeObject<T>(payload, ParseSettings);
            }
            catch (Exception ex)
            {
                Yes2Log.Warning($"Referrals: could not parse {typeof(T).Name} ({ex.Message})");
                return false;
            }
            return value != null;
        }

        private static Error UnreadableResultError(string context, string payload)
        {
            return new Error
            {
                Code = "PlatformError",
                Message = "Could not read the platform response: '" + payload + "'",
                Context = context
            };
        }

        private static int Register(Operation operation, Action<string> onSuccess, Action<Error> onError)
        {
            // Ids only need to be unique among requests still pending, so wrap
            // back to 1 rather than go negative after int.MaxValue calls.
            _nextRequestId = _nextRequestId == int.MaxValue ? 1 : _nextRequestId + 1;
            _pending[_nextRequestId] = new PendingRequest
            {
                Operation = operation,
                OnSuccess = onSuccess,
                OnError = onError
            };
            return _nextRequestId;
        }

        private static bool TryTake(Operation operation, int requestId, out PendingRequest request)
        {
            if (!_pending.TryGetValue(requestId, out request) || request.Operation != operation)
            {
                Yes2Log.Warning($"Referrals: dropped {operation} response for request {requestId}: no such request pending (already completed or unknown)");
                request = null;
                return false;
            }

            _pending.Remove(requestId);
            return true;
        }

        private static bool TryParseEnvelope(Operation operation, string message, out int requestId, out string payload)
        {
            requestId = 0;
            payload = null;

            int separator = message?.IndexOf(EnvelopeSeparator) ?? -1;
            if (separator <= 0
                || !int.TryParse(message.Substring(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out requestId))
            {
                Yes2Log.Warning($"Referrals: dropped {operation} response with no request id: '{message}'");
                return false;
            }

            payload = message.Substring(separator + 1);
            return true;
        }

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
