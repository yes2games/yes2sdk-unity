using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Yes2SDK
{
    /// <summary>
    /// In-App Purchase API for Yes2SDK.
    /// Backed by the platform payments API via the Core SDK (window.Yes2SDK.iap).
    /// On Yandex this maps to the Yandex Payments API (catalog, purchase, restore,
    /// consume). Result payloads are delivered to onSuccess as JSON strings.
    /// </summary>
    public class Yes2SDKIAP
    {
        #region Request Tracking

        /// <summary>The IAP operation a request belongs to.</summary>
        internal enum Operation
        {
            GetCatalog,
            Purchase,
            GetPurchases,
            ConsumePurchase
        }

        private sealed class PendingRequest
        {
            public Operation Operation;
            public Action<string> OnSuccess;
            public Action<Error> OnError;
        }

        // Every call gets its own id, carried through the JS bridge and echoed
        // back on its response. A shared slot per operation let a late response
        // to an abandoned call complete the retry that replaced it (#102).
        private static readonly Dictionary<int, PendingRequest> _pending = new Dictionary<int, PendingRequest>();
        private static int _nextRequestId;

        // Separates the request id from the payload in a bridge message.
        private const char EnvelopeSeparator = '|';

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_IAP_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_GetCatalogAsyncJS(int requestId);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_PurchaseAsyncJS(int requestId, string productId, string developerPayload);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_GetPurchasesAsyncJS(int requestId);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_ConsumePurchaseAsyncJS(int requestId, string purchaseToken);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether in-app purchases are supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IAP_IsSupportedJS();
#else
#if UNITY_EDITOR
            // With the IAP mock enabled, report supported so shop UI gated on
            // this can be tested in Play Mode.
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                Yes2Log.Log("Mock: IAP.IsSupported() — returning true (IAP mock enabled)");
                return true;
            }
#endif
            Yes2Log.Log("Mock: IAP.IsSupported() — returning false");
            return false;
#endif
        }

        /// <summary>
        /// Get the product catalog. onSuccess receives a JSON array of products.
        /// </summary>
        public void GetCatalogAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            int requestId = Register(Operation.GetCatalog, onSuccess, onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_GetCatalogAsyncJS(requestId);
#else
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                Yes2Log.Log("Mock: IAP.GetCatalogAsync() — returning mock catalog");
                CompleteSuccess(Operation.GetCatalog, requestId, Yes2SDKMockIAP.CatalogJson);
                return;
            }
#endif
            Yes2Log.Log("Mock: IAP.GetCatalogAsync() — returning empty catalog");
            CompleteSuccess(Operation.GetCatalog, requestId, "[]");
#endif
        }

        /// <summary>
        /// Initiate a purchase. onSuccess receives the purchase as a JSON object.
        /// </summary>
        /// <param name="productId">Product identifier to purchase.</param>
        /// <param name="onSuccess">Called with the purchase JSON on success.</param>
        /// <param name="onError">Called with an error on failure or cancellation.</param>
        /// <param name="developerPayload">Optional payload echoed back on the purchase.</param>
        public void PurchaseAsync(
            string productId,
            Action<string> onSuccess = null,
            Action<Error> onError = null,
            string developerPayload = null)
        {
#if UNITY_EDITOR
            // Reject overlapping mock purchases BEFORE registering the new
            // request: the overlay shows one dialog at a time, so a second
            // purchase would never get a dialog to resolve it.
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups
                && Yes2SDKMockOverlay.IsBusy)
            {
                Yes2Log.Log($"Mock: IAP.PurchaseAsync('{productId}') — rejected, another mock popup is open");
                onError?.Invoke(new Error
                {
                    Code = "PlatformError",
                    Message = "Another mock popup is already open",
                    Context = "Yes2SDK.IAP.PurchaseAsync"
                });
                return;
            }
#endif
            int requestId = Register(Operation.Purchase, onSuccess, onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_PurchaseAsyncJS(requestId, productId, developerPayload ?? string.Empty);
#else
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                // Failure simulation (Fail purchases toggle): resolve with a
                // platform-style error instead of showing the dialog.
                if (Yes2SDKEditorMock.IAPFailPurchases)
                {
                    Yes2Log.Log($"Mock: IAP.PurchaseAsync('{productId}') — simulated failure");
                    CompleteError(Operation.Purchase, requestId, new Error
                    {
                        Code = "PlatformError",
                        Message = "Simulated purchase failure (mock)",
                        Context = "Yes2SDK.IAP.PurchaseAsync"
                    });
                    return;
                }

                if (Yes2SDKMockOverlay.ShowPurchase(requestId, productId, developerPayload))
                {
                    Yes2Log.Log($"Mock: IAP.PurchaseAsync('{productId}') — showing purchase dialog");
                    return;
                }
            }
#endif
            Yes2Log.Log($"Mock: IAP.PurchaseAsync('{productId}') — FeatureNotSupported");
            CompleteError(Operation.Purchase, requestId, FeatureNotSupportedError("Yes2SDK.IAP.PurchaseAsync"));
#endif
        }

        /// <summary>
        /// Restore the player's purchases. onSuccess receives a JSON array of
        /// purchases — call this on launch so returning players keep what they own.
        /// </summary>
        public void GetPurchasesAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            int requestId = Register(Operation.GetPurchases, onSuccess, onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_GetPurchasesAsyncJS(requestId);
#else
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                Yes2Log.Log("Mock: IAP.GetPurchasesAsync() — returning mock purchases");
                CompleteSuccess(Operation.GetPurchases, requestId, Yes2SDKMockIAP.PurchasesJson);
                return;
            }
#endif
            Yes2Log.Log("Mock: IAP.GetPurchasesAsync() — returning empty list");
            CompleteSuccess(Operation.GetPurchases, requestId, "[]");
#endif
        }

        /// <summary>
        /// Consume a purchase (for consumable products) so it can be bought again.
        /// </summary>
        public void ConsumePurchaseAsync(string purchaseToken, Action onSuccess = null, Action<Error> onError = null)
        {
            Action<string> consumeSuccess = null;
            if (onSuccess != null) consumeSuccess = _ => onSuccess();
            int requestId = Register(Operation.ConsumePurchase, consumeSuccess, onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_ConsumePurchaseAsyncJS(requestId, purchaseToken);
#else
#if UNITY_EDITOR
            // Remove the purchase from the mock session list so a consumable
            // bought via the mock dialog can be bought again.
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                Yes2SDKMockIAP.Consume(purchaseToken);
            }
#endif
            Yes2Log.Log($"Mock: IAP.ConsumePurchaseAsync('{purchaseToken}') — success");
            CompleteSuccess(Operation.ConsumePurchase, requestId, string.Empty);
#endif
        }

        #endregion

        #region Internal Completion (called by Bridge and the Editor mock)

        /// <summary>
        /// Bridge entry for a success message. The message is
        /// "&lt;requestId&gt;|&lt;payload&gt;", as written by Yes2SDKIAP.jslib.
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
            // request's callbacks (#103). A duplicate or stale response finds
            // nothing and is dropped.
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

        /// <summary>Requests still waiting for a response.</summary>
        internal static int PendingCountForTests => _pending.Count;

        /// <summary>Drops every pending request so tests start clean.</summary>
        internal static void ResetIapStateForTests()
        {
            _pending.Clear();
        }

        /// <summary>Builds the message the jslib sends for a response.</summary>
        internal static string EnvelopeForTests(int requestId, string payload)
        {
            return requestId.ToString(CultureInfo.InvariantCulture) + EnvelopeSeparator + payload;
        }

        #endregion

        #region Private Helpers

#if UNITY_EDITOR
        // The mock purchase dialog (Yes2SDKMockOverlay) can leave a purchase
        // pending when Play Mode is stopped mid-dialog. With Domain Reload
        // disabled statics survive into the next play, so clear the stored
        // callbacks explicitly on each play.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorState()
        {
            _pending.Clear();
        }
#endif

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
                Yes2Log.Warning($"IAP: dropped {operation} response for request {requestId}: no such request pending (already completed or unknown)");
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
                Yes2Log.Warning($"IAP: dropped {operation} response with no request id: '{message}'");
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
