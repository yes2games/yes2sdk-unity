using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yes2SDK
{
    /// <summary>
    /// Notifications API: schedule and cancel notifications that bring the player back to the game.
    /// Reports FeatureNotSupported on platforms without notifications; check <see cref="IsSupported"/>.
    /// Some platforms only deliver notifications to registered players and fail the call for guests
    /// (error code "PLAYER_NOT_AUTHENTICATED").
    /// </summary>
    public class Yes2SDKNotifications
    {
        #region Request Tracking

        /// <summary>The notifications operation a request belongs to.</summary>
        internal enum Operation
        {
            Schedule,
            Cancel,
            CancelAll
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

        private const string ScheduleContext = "Yes2SDK.Notifications.ScheduleAsync";
        private const string CancelContext = "Yes2SDK.Notifications.CancelAsync";
        private const string CancelAllContext = "Yes2SDK.Notifications.CancelAllAsync";

        private const long MillisecondsPerDay = 86_400_000L;

        private static readonly JsonSerializerSettings ParseSettings = new JsonSerializerSettings
        {
            // Keep date-like strings in game data exactly as written.
            DateParseHandling = DateParseHandling.None
        };

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Notifications_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Notifications_ScheduleAsyncJS(int requestId, string optionsJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Notifications_CancelAsyncJS(int requestId, string notificationId);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Notifications_CancelAllAsyncJS(int requestId);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether notifications are supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Notifications_IsSupportedJS();
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                Yes2Log.Log("Mock: Notifications.IsSupported() - returning true (notifications mock enabled)");
                return true;
            }
#endif
            Yes2Log.Log("Mock: Notifications.IsSupported() - returning false");
            return false;
#endif
        }

        /// <summary>
        /// Schedule a notification. onSuccess receives the notification the platform accepted;
        /// keep its <see cref="ScheduledNotification.Id"/> to cancel it later. Null options, or
        /// <see cref="NotificationOptions.Data"/> that cannot be written as JSON (for example a
        /// dictionary that contains itself), call onError synchronously with InvalidParams; other
        /// option rules are checked by the platform and fail with InvalidParams through onError.
        /// A guest player gets an error with code "PLAYER_NOT_AUTHENTICATED" on platforms that only
        /// notify registered players.
        /// </summary>
        public void ScheduleAsync(
            NotificationOptions options,
            Action<ScheduledNotification> onSuccess = null,
            Action<Error> onError = null)
        {
            if (!TrySerialize(options, onError, out string json)) return;

            int requestId = Register(Operation.Schedule, TypedSchedule(onSuccess, onError), onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Notifications_ScheduleAsyncJS(requestId, json);
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                MockSchedule(requestId, options, Yes2SDKEditorMock.IsRegisteredNow);
                return;
            }
#endif
            Yes2Log.Log($"Mock: Notifications.ScheduleAsync('{options.Title}') - FeatureNotSupported");
            CompleteError(Operation.Schedule, requestId, FeatureNotSupportedError(ScheduleContext));
#endif
        }

        /// <summary>
        /// Schedule a notification after <paramref name="delaySec"/> seconds. onSuccess receives the
        /// notification id. <paramref name="delaySec"/> must be positive: zero or less fails with
        /// InvalidParams. A null <paramref name="body"/> is sent as an empty string, which platforms
        /// that deliver notifications reject. <paramref name="dataJson"/> must be a JSON object;
        /// anything else is dropped with a warning. Prefer the <see cref="NotificationOptions"/> overload.
        /// </summary>
        public void ScheduleAsync(string title, string body, int delaySec, string dataJson, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            ScheduleLegacy(title, body, delaySec, dataJson, onSuccess, onError, ScheduleAsync);
        }

        /// <summary>
        /// Cancel a scheduled notification by the id returned when it was scheduled.
        /// </summary>
        public void CancelAsync(string notificationId, Action onSuccess = null, Action<Error> onError = null)
        {
            int requestId = Register(Operation.Cancel, Untyped(onSuccess), onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Notifications_CancelAsyncJS(requestId, notificationId ?? string.Empty);
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                MockCancel(requestId, notificationId);
                return;
            }
#endif
            Yes2Log.Log($"Mock: Notifications.CancelAsync('{notificationId}') - FeatureNotSupported");
            CompleteError(Operation.Cancel, requestId, FeatureNotSupportedError(CancelContext));
#endif
        }

        /// <summary>
        /// Cancel every notification this game scheduled. Some platforms can only cancel the
        /// notifications scheduled during the current session.
        /// </summary>
        public void CancelAllAsync(Action onSuccess = null, Action<Error> onError = null)
        {
            int requestId = Register(Operation.CancelAll, Untyped(onSuccess), onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Notifications_CancelAllAsyncJS(requestId);
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                MockCancelAll(requestId);
                return;
            }
#endif
            Yes2Log.Log("Mock: Notifications.CancelAllAsync() - FeatureNotSupported");
            CompleteError(Operation.CancelAll, requestId, FeatureNotSupportedError(CancelAllContext));
#endif
        }

        // Task-returning overloads.

        public Task<ScheduledNotification> ScheduleAsync(NotificationOptions options, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<ScheduledNotification>(
                (success, error) => ScheduleAsync(options, success, error),
                cancellationToken);

        public Task CancelAsync(string notificationId, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask(
                (success, error) => CancelAsync(notificationId, success, error),
                cancellationToken);

        public Task CancelAllAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask(
                (success, error) => CancelAllAsync(success, error),
                cancellationToken);

        #endregion

        #region Internal Completion (called by Bridge and the Editor mock)

        /// <summary>
        /// Bridge entry for a success message. The message is
        /// "&lt;requestId&gt;|&lt;payload&gt;", as written by Yes2SDKNotifications.jslib.
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

        /// <summary>
        /// The legacy (title, body, delaySec, dataJson) overload, with the scheduler passed in so
        /// tests can check what it forwards.
        /// </summary>
        internal static void ScheduleLegacy(
            string title,
            string body,
            int delaySec,
            string dataJson,
            Action<string> onSuccess,
            Action<Error> onError,
            Action<NotificationOptions, Action<ScheduledNotification>, Action<Error>> schedule)
        {
            var options = new NotificationOptions
            {
                Title = title,
                Body = body,
                DelaySeconds = delaySec,
                Data = ParseLegacyData(dataJson)
            };
            schedule(options, notification => onSuccess?.Invoke(notification.Id), onError);
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

        /// <summary>Registers a schedule request with the same typed parsing the public API uses.</summary>
        internal static int RegisterScheduleForTests(Action<ScheduledNotification> onSuccess, Action<Error> onError)
        {
            return Register(Operation.Schedule, TypedSchedule(onSuccess, onError), onError);
        }

        /// <summary>Requests still waiting for a response.</summary>
        internal static int PendingCountForTests => _pending.Count;

        /// <summary>Drops every pending request (and the mock's scheduled list) so tests start clean.</summary>
        internal static void ResetNotificationsStateForTests()
        {
            _pending.Clear();
#if UNITY_EDITOR
            _mockScheduled.Clear();
            _mockIdCounter = 0;
#endif
        }

        /// <summary>Builds the message the jslib sends for a response.</summary>
        internal static string EnvelopeForTests(int requestId, string payload)
        {
            return requestId.ToString(CultureInfo.InvariantCulture) + EnvelopeSeparator + payload;
        }

#if UNITY_EDITOR
        /// <summary>Runs the Editor mock schedule (no Play Mode needed).</summary>
        internal static void MockScheduleForTests(
            NotificationOptions options,
            bool registered,
            Action<ScheduledNotification> onSuccess,
            Action<Error> onError)
        {
            if (!TrySerialize(options, onError, out _)) return;
            int requestId = Register(Operation.Schedule, TypedSchedule(onSuccess, onError), onError);
            MockSchedule(requestId, options, registered);
        }

        /// <summary>Runs the Editor mock cancel.</summary>
        internal static void MockCancelForTests(string notificationId, Action onSuccess, Action<Error> onError)
        {
            MockCancel(Register(Operation.Cancel, Untyped(onSuccess), onError), notificationId);
        }

        /// <summary>Runs the Editor mock cancel all.</summary>
        internal static void MockCancelAllForTests(Action onSuccess, Action<Error> onError)
        {
            MockCancelAll(Register(Operation.CancelAll, Untyped(onSuccess), onError));
        }

        /// <summary>Notifications the Editor mock holds for this session, in schedule order.</summary>
        internal static IReadOnlyList<ScheduledNotification> MockScheduledForTests => _mockScheduled;
#endif

        #endregion

        #region Editor Mock

#if UNITY_EDITOR
        // Notifications scheduled during this Play Mode session.
        private static readonly List<ScheduledNotification> _mockScheduled = new List<ScheduledNotification>();
        private static int _mockIdCounter;

        private static bool MockActive =>
            Yes2SDKEditorMock.PlatformServicesEnabled && Yes2SDKEditorMock.CanShowPopups;

        // Mirrors the platform order: option rules first, then the registered player check,
        // then the platform limits.
        private static void MockSchedule(int requestId, NotificationOptions options, bool registered)
        {
            if (!NotificationOptions.TryValidate(options, out string message))
            {
                MockInvalidParams(requestId, message);
                return;
            }

            if (!registered)
            {
                Yes2Log.Log("Mock: Notifications.ScheduleAsync() - PLAYER_NOT_AUTHENTICATED: the player is a guest");
                CompleteError(Operation.Schedule, requestId, new Error
                {
                    Code = "PLAYER_NOT_AUTHENTICATED",
                    Message = "Notifications require a registered player (mock: the player is a guest)",
                    Context = ScheduleContext
                });
                return;
            }

            if (!NotificationOptions.TryValidatePlatformLimits(options, out message))
            {
                MockInvalidParams(requestId, message);
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long scheduledAt = options.DelaySeconds.HasValue
                ? now + options.DelaySeconds.Value * 1000L
                : now + options.ScheduledInDays.GetValueOrDefault() * MillisecondsPerDay;
            var notification = new ScheduledNotification
            {
                Id = options.Id ?? "mock-notification-" + (++_mockIdCounter),
                Title = options.Title,
                Body = options.Body,
                ScheduledAt = scheduledAt
            };

            // Scheduling again with the same id replaces the earlier one.
            _mockScheduled.RemoveAll(n => n.Id == notification.Id);
            _mockScheduled.Add(notification);

            Yes2Log.Log($"Mock: Notifications.ScheduleAsync('{notification.Id}') - scheduled");
            CompleteSuccess(Operation.Schedule, requestId, JsonConvert.SerializeObject(notification));
        }

        private static void MockInvalidParams(int requestId, string message)
        {
            Yes2Log.Log($"Mock: Notifications.ScheduleAsync() - InvalidParams: {message}");
            CompleteError(Operation.Schedule, requestId, InvalidScheduleParams(message));
        }

        private static void MockCancel(int requestId, string notificationId)
        {
            if (notificationId == null || notificationId.Trim().Length == 0)
            {
                CompleteError(Operation.Cancel, requestId, new Error
                {
                    Code = "InvalidParams",
                    Message = "notificationId must be a non-empty string",
                    Context = CancelContext
                });
                return;
            }

            int removed = _mockScheduled.RemoveAll(n => n.Id == notificationId);
            Yes2Log.Log($"Mock: Notifications.CancelAsync('{notificationId}') - removed {removed}");
            CompleteSuccess(Operation.Cancel, requestId, string.Empty);
        }

        private static void MockCancelAll(int requestId)
        {
            Yes2Log.Log($"Mock: Notifications.CancelAllAsync() - removed {_mockScheduled.Count}");
            _mockScheduled.Clear();
            CompleteSuccess(Operation.CancelAll, requestId, string.Empty);
        }

        // Pending requests and mock notifications can survive a stopped Play
        // Mode when Domain Reload is disabled, so clear them on each play.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorState()
        {
            _pending.Clear();
            _mockScheduled.Clear();
            _mockIdCounter = 0;
        }
#endif

        #endregion

        #region Private Helpers

        private static Action<string> TypedSchedule(Action<ScheduledNotification> onSuccess, Action<Error> onError)
        {
            return payload =>
            {
                ScheduledNotification? notification = null;
                if (!string.IsNullOrEmpty(payload))
                {
                    try
                    {
                        notification = JsonConvert.DeserializeObject<ScheduledNotification?>(payload, ParseSettings);
                    }
                    catch (Exception ex)
                    {
                        Yes2Log.Warning($"Notifications: could not parse ScheduledNotification ({ex.GetType().Name})");
                    }
                }

                if (notification.HasValue)
                {
                    onSuccess?.Invoke(notification.Value);
                }
                else
                {
                    // The raw payload stays out of the message: it can carry game data.
                    onError?.Invoke(new Error
                    {
                        Code = "Unknown",
                        Message = "Could not read the scheduled notification returned by the platform",
                        Context = ScheduleContext
                    });
                }
            };
        }

        /// <summary>
        /// Writes the options JSON before any request is registered. Null options or options that
        /// cannot be written (a cyclic or unsupported Data value) complete onError synchronously
        /// with InvalidParams, so no request is left pending.
        /// </summary>
        private static bool TrySerialize(NotificationOptions options, Action<Error> onError, out string json)
        {
            json = null;
            if (options == null)
            {
                Yes2Log.Warning("Notifications.ScheduleAsync: options must not be null");
                onError?.Invoke(InvalidScheduleParams("options must be a valid object"));
                return false;
            }

            try
            {
                json = options.ToJson();
                return true;
            }
            catch (Exception ex)
            {
                Yes2Log.Warning($"Notifications.ScheduleAsync: options could not be written as JSON ({ex.GetType().Name})");
            }

            onError?.Invoke(InvalidScheduleParams(
                "options could not be written as JSON; check that Data holds no cycles or unsupported values"));
            return false;
        }

        private static Error InvalidScheduleParams(string message)
        {
            return new Error { Code = "InvalidParams", Message = message, Context = ScheduleContext };
        }

        private static Action<string> Untyped(Action onSuccess)
        {
            if (onSuccess == null) return null;
            return _ => onSuccess();
        }

        private static Dictionary<string, object> ParseLegacyData(string dataJson)
        {
            if (string.IsNullOrEmpty(dataJson)) return null;
            try
            {
                if (JsonConvert.DeserializeObject<JToken>(dataJson, ParseSettings) is JObject data)
                {
                    return data.ToObject<Dictionary<string, object>>();
                }
            }
            catch (Exception ex)
            {
                Yes2Log.Warning($"Notifications.ScheduleAsync: dataJson is not valid JSON, sending no data ({ex.Message})");
                return null;
            }
            Yes2Log.Warning("Notifications.ScheduleAsync: dataJson must be a JSON object, sending no data");
            return null;
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
                Yes2Log.Warning($"Notifications: dropped {operation} response for request {requestId}: no such request pending (already completed or unknown)");
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
                Yes2Log.Warning($"Notifications: dropped {operation} response with no request id: '{message}'");
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
                Message = "Notifications are not supported on the current platform",
                Context = context
            };
        }

        #endregion
    }
}
