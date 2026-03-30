using System;

namespace Yes2SDK
{
    /// <summary>
    /// In-App Purchase API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKIAP : Yes2SDKStubModule
    {
        protected override string FeatureName => "IAP";
        protected override string ModuleName => "IAP";

        public void GetCatalogAsync(Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetCatalogAsync));

        public void PurchaseAsync(string productId, Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(PurchaseAsync), productId);

        public void GetPurchasesAsync(Action<string> onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(GetPurchasesAsync));

        public void ConsumePurchaseAsync(string purchaseToken, Action onSuccess = null, Action<Error> onError = null)
            => Stub(onError, nameof(ConsumePurchaseAsync), purchaseToken);
    }
}
