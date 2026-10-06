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
    /// Context API for Yes2SDK.
    /// <see cref="ShareAsync(string, string, Action, Action{Error})"/> opens the platform share
    /// sheet with an image where the platform supports it. Context switching
    /// (<see cref="SwitchAsync"/>, <see cref="ChooseAsync"/>, <see cref="CreateAsync"/>,
    /// <see cref="GetContext"/>) is not supported and reports FeatureNotSupported.
    /// Do not gate sharing on <c>IsSupported()</c>: it reports context switching and can be
    /// false on a platform where sharing works. Call ShareAsync and handle onError instead.
    /// </summary>
    public class Yes2SDKContext : Yes2SDKStubModule
    {
        protected override string FeatureName => "Context";
        protected override string ModuleName => "Context";

        #region Request Tracking

        /// <summary>The context operation a request belongs to.</summary>
        internal enum Operation
        {
            Share
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

        private const string ShareContext = "Yes2SDK.Context.ShareAsync";
        private const string ShareIntent = "SHARE";
        private const string PngDataUrlPrefix = "data:image/png;base64,";

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_Context_ShareAsyncJS(int requestId, string payloadJson);
#endif

        #endregion

        #region Public API

        /// <summary>Not supported. Always returns null.</summary>
        public string GetContext()
        {
            StubLog(nameof(GetContext));
            return null;
        }

        /// <summary>Not supported. Calls onError with FeatureNotSupported.</summary>
        public void SwitchAsync(string contextId, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(SwitchAsync), contextId);

        /// <summary>Not supported. Calls onError with FeatureNotSupported.</summary>
        public void ChooseAsync(Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(ChooseAsync));

        /// <summary>Not supported. Calls onError with FeatureNotSupported.</summary>
        public void CreateAsync(string playerId, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(CreateAsync), playerId);

        /// <summary>
        /// Open the platform share sheet with an image.
        /// <paramref name="imageBase64"/> is a PNG data URL ("data:image/png;base64,...", build one
        /// from a texture with <see cref="Yes2SDKImage.ToPngDataUrl(Texture2D)"/>) or raw base64 PNG
        /// data, which gets the PNG data URL prefix. Pass null to let the platform capture the
        /// game screen where it can. <paramref name="text"/> may be ignored by some platforms.
        /// onSuccess means the share sheet completed or was dismissed: a player closing it
        /// without sharing is not reported. Platforms without sharing call onError with
        /// FeatureNotSupported. Do not gate on <c>IsSupported()</c>, which can be false where
        /// sharing works.
        /// </summary>
        public void ShareAsync(string text, string imageBase64, Action onSuccess = null, Action<Error> onError = null)
            => ShareAsync(text, imageBase64, null, onSuccess, onError);

        /// <summary>
        /// Open the platform share sheet with an image and an entry payload.
        /// <paramref name="data"/> is handed to the player who opens the share, through
        /// <c>Yes2SDK.Session.GetEntryPointData()</c>. Data that cannot be serialized to JSON
        /// (a cyclic graph, or a Unity type such as Vector3) calls onError synchronously with
        /// InvalidParams. Everything else is as in
        /// <see cref="ShareAsync(string, string, Action, Action{Error})"/>.
        /// </summary>
        public void ShareAsync(
            string text,
            string imageBase64,
            Dictionary<string, object> data,
            Action onSuccess = null,
            Action<Error> onError = null)
        {
            string image = NormalizeImage(imageBase64);

            // Serialize before registering: data the serializer cannot handle
            // fails here, synchronously, and never leaves a pending request.
            if (!TrySerializeSharePayload(text, image, data, out string payloadJson, out Error serializeError))
            {
                onError?.Invoke(serializeError);
                return;
            }

            int requestId = Register(Operation.Share, TypedShare(onSuccess), onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Context_ShareAsyncJS(requestId, payloadJson);
#else
#if UNITY_EDITOR
            if (MockActive)
            {
                MockShare(requestId, image != null);
                return;
            }
#endif
            Yes2Log.Log("Mock: Context.ShareAsync() - FeatureNotSupported");
            CompleteError(Operation.Share, requestId, new Error
            {
                Code = "FeatureNotSupported",
                Message = "Sharing is not supported on the current platform",
                Context = ShareContext
            });
#endif
        }

        /// <summary>
        /// Open the platform share sheet with a texture, encoded with
        /// <see cref="Yes2SDKImage.ToPngDataUrl(Texture2D)"/>. A texture that is null, not
        /// readable (enable Read/Write), or larger than 2 MiB once encoded calls onError
        /// synchronously with InvalidParams. Everything else is as in
        /// <see cref="ShareAsync(string, string, Dictionary{string, object}, Action, Action{Error})"/>.
        /// </summary>
        public void ShareAsync(
            Texture2D image,
            string text = null,
            Dictionary<string, object> data = null,
            Action onSuccess = null,
            Action<Error> onError = null)
        {
            string dataUrl = Yes2SDKImage.ToPngDataUrl(image);
            if (dataUrl == null)
            {
                Yes2Log.Warning("Context.ShareAsync: the image could not be encoded as a PNG data URL");
                onError?.Invoke(new Error
                {
                    Code = "InvalidParams",
                    Message = "image must be a readable texture that encodes to at most 2 MiB",
                    Context = ShareContext
                });
                return;
            }

            ShareAsync(text, dataUrl, data, onSuccess, onError);
        }

        // Task-returning overloads.

        /// <summary>Task overload of <see cref="ShareAsync(string, string, Dictionary{string, object}, Action, Action{Error})"/>.</summary>
        public Task ShareAsync(
            string text,
            string imageBase64,
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask(
                (success, error) => ShareAsync(text, imageBase64, data, success, error),
                cancellationToken);

        /// <summary>Task overload of <see cref="ShareAsync(Texture2D, string, Dictionary{string, object}, Action, Action{Error})"/>.</summary>
        public Task ShareAsync(
            Texture2D image,
            string text,
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask(
                (success, error) => ShareAsync(image, text, data, success, error),
                cancellationToken);

        #endregion

        #region Internal Completion (called by Bridge and the Editor mock)

        /// <summary>
        /// Bridge entry for a success message. The message is
        /// "&lt;requestId&gt;|" (no payload), as written by Yes2SDKContext.jslib.
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
            // starts the next share, or throws, cannot touch the next request's
            // callbacks. A duplicate or stale response finds nothing and is dropped.
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
        /// Registers a share request without sending it, so tests can deliver its
        /// response later and in any order, as a delayed platform would.
        /// </summary>
        internal static int RegisterShareForTests(Action onSuccess, Action<Error> onError)
        {
            return Register(Operation.Share, TypedShare(onSuccess), onError);
        }

        /// <summary>Requests still waiting for a response.</summary>
        internal static int PendingCountForTests => _pending.Count;

        /// <summary>Drops every pending request so tests start clean.</summary>
        internal static void ResetContextStateForTests()
        {
            _pending.Clear();
        }

        /// <summary>Builds the message the jslib sends for a response.</summary>
        internal static string EnvelopeForTests(int requestId, string payload)
        {
            return requestId.ToString(CultureInfo.InvariantCulture) + EnvelopeSeparator + payload;
        }

#if UNITY_EDITOR
        /// <summary>Runs the Editor mock share (no Play Mode needed).</summary>
        internal static void MockShareForTests(
            string text,
            string imageBase64,
            Dictionary<string, object> data,
            Action onSuccess,
            Action<Error> onError)
        {
            string image = NormalizeImage(imageBase64);
            if (!TrySerializeSharePayload(text, image, data, out _, out Error serializeError))
            {
                onError?.Invoke(serializeError);
                return;
            }
            int requestId = Register(Operation.Share, TypedShare(onSuccess), onError);
            MockShare(requestId, image != null);
        }
#endif

        #endregion

        #region Editor Mock

#if UNITY_EDITOR
        private static bool MockActive =>
            Yes2SDKEditorMock.PlatformServicesEnabled && Yes2SDKEditorMock.CanShowPopups;

        private static void MockShare(int requestId, bool hasImage)
        {
            // The platform reports no cancel signal, so the mock always completes.
            Yes2Log.Log(hasImage
                ? "Mock: Context.ShareAsync() - shared the image"
                : "Mock: Context.ShareAsync() - shared a capture of the game screen");
            CompleteSuccess(Operation.Share, requestId, string.Empty);
        }

        // Pending requests can survive a stopped Play Mode when Domain Reload
        // is disabled, so clear them on each play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorState()
        {
            _pending.Clear();
        }
#endif

        #endregion

        #region Private Helpers

        private static Action<string> TypedShare(Action onSuccess)
        {
            // The share resolves without a result, so the payload is ignored.
            return _ => onSuccess?.Invoke();
        }

        /// <summary>
        /// Returns the image as a data URL: a data URL is kept as is, raw base64
        /// gets the PNG data URL prefix, and null or blank gives null (no image).
        /// </summary>
        internal static string NormalizeImage(string imageBase64)
        {
            if (string.IsNullOrWhiteSpace(imageBase64)) return null;
            string trimmed = imageBase64.Trim();
            return trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : PngDataUrlPrefix + trimmed;
        }

        /// <summary>
        /// Serializes the share payload ({ intent, image?, text?, data? }) for the
        /// platform, leaving out absent fields. Returns false with an InvalidParams
        /// error when data cannot be serialized (a cyclic graph, or a value such as
        /// a Unity vector that the serializer rejects).
        /// </summary>
        internal static bool TrySerializeSharePayload(
            string text,
            string image,
            Dictionary<string, object> data,
            out string json,
            out Error error)
        {
            var payload = new Dictionary<string, object> { ["intent"] = ShareIntent };
            if (image != null) payload["image"] = image;
            if (!string.IsNullOrEmpty(text)) payload["text"] = text;
            if (data != null) payload["data"] = data;

            try
            {
                json = JsonConvert.SerializeObject(payload);
                error = default;
                return true;
            }
            catch (Exception ex)
            {
                Yes2Log.Warning($"Context.ShareAsync: data could not be serialized ({ex.Message})");
                json = null;
                error = new Error
                {
                    Code = "InvalidParams",
                    Message = "data could not be serialized",
                    Context = ShareContext
                };
                return false;
            }
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
                Yes2Log.Warning($"Context: dropped {operation} response for request {requestId}: no such request pending (already completed or unknown)");
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
                Yes2Log.Warning($"Context: dropped {operation} response with no request id: '{message}'");
                return false;
            }

            payload = message.Substring(separator + 1);
            return true;
        }

        #endregion
    }
}
