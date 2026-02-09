using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// In-App Purchase API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKIAP
    {
        /// <summary>
        /// Whether IAP is supported on the current platform.
        /// </summary>
        public bool IsSupported() => false;

        /// <summary>
        /// Get the product catalog.
        /// </summary>
        public void GetCatalogAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: GetCatalogAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.IAP.GetCatalogAsync"));
        }

        /// <summary>
        /// Purchase a product.
        /// </summary>
        public void PurchaseAsync(string productId, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: PurchaseAsync({productId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.IAP.PurchaseAsync"));
        }

        /// <summary>
        /// Get all unconsumed purchases.
        /// </summary>
        public void GetPurchasesAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: GetPurchasesAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.IAP.GetPurchasesAsync"));
        }

        /// <summary>
        /// Consume a purchase.
        /// </summary>
        public void ConsumePurchaseAsync(string purchaseToken, Action onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: ConsumePurchaseAsync({purchaseToken}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.IAP.ConsumePurchaseAsync"));
        }

        private static bool IsEditor()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static Error NotSupportedError(string context) => new Error
        {
            Code = "FeatureNotSupported",
            Message = "IAP is not supported on the current platform",
            Context = context
        };
    }
}
