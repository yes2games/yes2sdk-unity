using System;
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
        #region Static Callback Fields

        private static Action<string> _getCatalogSuccessCallback;
        private static Action<Error> _getCatalogErrorCallback;
        private static Action<string> _purchaseSuccessCallback;
        private static Action<Error> _purchaseErrorCallback;
        private static Action<string> _getPurchasesSuccessCallback;
        private static Action<Error> _getPurchasesErrorCallback;
        private static Action _consumeSuccessCallback;
        private static Action<Error> _consumeErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_IAP_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_GetCatalogAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_PurchaseAsyncJS(string productId, string developerPayload);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_GetPurchasesAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_IAP_ConsumePurchaseAsyncJS(string purchaseToken);
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
            _getCatalogSuccessCallback = onSuccess;
            _getCatalogErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_GetCatalogAsyncJS();
#else
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                Yes2Log.Log("Mock: IAP.GetCatalogAsync() — returning mock catalog");
                InvokeGetCatalogSuccess(Yes2SDKMockIAP.CatalogJson);
                return;
            }
#endif
            Yes2Log.Log("Mock: IAP.GetCatalogAsync() — returning empty catalog");
            InvokeGetCatalogSuccess("[]");
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
            // Reject overlapping mock purchases BEFORE storing the new
            // callbacks — overwriting them here would silently orphan the
            // purchase dialog still waiting on screen.
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
            _purchaseSuccessCallback = onSuccess;
            _purchaseErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_PurchaseAsyncJS(productId, developerPayload ?? string.Empty);
#else
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups
                && Yes2SDKMockOverlay.ShowPurchase(productId, developerPayload))
            {
                Yes2Log.Log($"Mock: IAP.PurchaseAsync('{productId}') — showing purchase dialog");
                return;
            }
#endif
            Yes2Log.Log($"Mock: IAP.PurchaseAsync('{productId}') — FeatureNotSupported");
            InvokePurchaseError(FeatureNotSupportedError("Yes2SDK.IAP.PurchaseAsync"));
#endif
        }

        /// <summary>
        /// Restore the player's purchases. onSuccess receives a JSON array of
        /// purchases — call this on launch so returning players keep what they own.
        /// </summary>
        public void GetPurchasesAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getPurchasesSuccessCallback = onSuccess;
            _getPurchasesErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_GetPurchasesAsyncJS();
#else
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.IAPEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                Yes2Log.Log("Mock: IAP.GetPurchasesAsync() — returning mock purchases");
                InvokeGetPurchasesSuccess(Yes2SDKMockIAP.PurchasesJson);
                return;
            }
#endif
            Yes2Log.Log("Mock: IAP.GetPurchasesAsync() — returning empty list");
            InvokeGetPurchasesSuccess("[]");
#endif
        }

        /// <summary>
        /// Consume a purchase (for consumable products) so it can be bought again.
        /// </summary>
        public void ConsumePurchaseAsync(string purchaseToken, Action onSuccess = null, Action<Error> onError = null)
        {
            _consumeSuccessCallback = onSuccess;
            _consumeErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_IAP_ConsumePurchaseAsyncJS(purchaseToken);
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
            InvokeConsumePurchaseSuccess();
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeGetCatalogSuccess(string catalogJson)
        {
            _getCatalogSuccessCallback?.Invoke(catalogJson);
            _getCatalogSuccessCallback = null;
            _getCatalogErrorCallback = null;
        }

        internal static void InvokeGetCatalogError(Error error)
        {
            _getCatalogErrorCallback?.Invoke(error);
            _getCatalogSuccessCallback = null;
            _getCatalogErrorCallback = null;
        }

        internal static void InvokePurchaseSuccess(string purchaseJson)
        {
            _purchaseSuccessCallback?.Invoke(purchaseJson);
            _purchaseSuccessCallback = null;
            _purchaseErrorCallback = null;
        }

        internal static void InvokePurchaseError(Error error)
        {
            _purchaseErrorCallback?.Invoke(error);
            _purchaseSuccessCallback = null;
            _purchaseErrorCallback = null;
        }

        internal static void InvokeGetPurchasesSuccess(string purchasesJson)
        {
            _getPurchasesSuccessCallback?.Invoke(purchasesJson);
            _getPurchasesSuccessCallback = null;
            _getPurchasesErrorCallback = null;
        }

        internal static void InvokeGetPurchasesError(Error error)
        {
            _getPurchasesErrorCallback?.Invoke(error);
            _getPurchasesSuccessCallback = null;
            _getPurchasesErrorCallback = null;
        }

        internal static void InvokeConsumePurchaseSuccess()
        {
            _consumeSuccessCallback?.Invoke();
            _consumeSuccessCallback = null;
            _consumeErrorCallback = null;
        }

        internal static void InvokeConsumePurchaseError(Error error)
        {
            _consumeErrorCallback?.Invoke(error);
            _consumeSuccessCallback = null;
            _consumeErrorCallback = null;
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
            _getCatalogSuccessCallback = null;
            _getCatalogErrorCallback = null;
            _purchaseSuccessCallback = null;
            _purchaseErrorCallback = null;
            _getPurchasesSuccessCallback = null;
            _getPurchasesErrorCallback = null;
            _consumeSuccessCallback = null;
            _consumeErrorCallback = null;
        }
#endif

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
