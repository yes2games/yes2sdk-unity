#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Editor-only mock IAP backend used by Yes2SDKIAP in Play Mode when the
    /// "Mock in-app purchases" toggle is on. Payload shapes mirror the
    /// platform Product / Purchase JSON, so integration code parses the same
    /// fields it will see on a real platform build.
    /// </summary>
    internal static class Yes2SDKMockIAP
    {
        internal class MockProduct
        {
            [JsonProperty("productId")] public string ProductId;
            [JsonProperty("title")] public string Title;
            [JsonProperty("description")] public string Description;
            [JsonProperty("imageUri")] public string ImageUri = "";
            [JsonProperty("price")] public string Price;
            [JsonProperty("priceCurrencyCode")] public string PriceCurrencyCode = "USD";
            [JsonProperty("priceAmount")] public int PriceAmount;
        }

        internal class MockPurchase
        {
            [JsonProperty("purchaseToken")] public string PurchaseToken;
            [JsonProperty("productId")] public string ProductId;
            [JsonProperty("paymentId")] public string PaymentId;
            [JsonProperty("purchaseTime")] public string PurchaseTime;
            [JsonProperty("developerPayload", NullValueHandling = NullValueHandling.Ignore)]
            public string DeveloperPayload;
            [JsonProperty("signedRequest")] public string SignedRequest = "mock-signed-request";
            [JsonProperty("isSandbox")] public bool IsSandbox = true;
        }

        internal class MockOffer
        {
            [JsonProperty("priceAmount")] public double PriceAmount;
            [JsonProperty("durationPeriods")] public int DurationPeriods;
        }

        // Same JSON shape as the platform Subscription, so games parse the
        // fields they will see on a real platform build.
        internal class MockSubscription
        {
            [JsonProperty("productId")] public string ProductId;
            [JsonProperty("title")] public string Title;
            [JsonProperty("description")] public string Description;
            [JsonProperty("price")] public string Price;
            [JsonProperty("priceAmount")] public double PriceAmount;
            [JsonProperty("priceCurrencyCode")] public string PriceCurrencyCode = "USD";
            [JsonProperty("billingPeriod")] public string BillingPeriod;
            [JsonProperty("isActive")] public bool IsActive;
            [JsonProperty("trialEligible")] public bool TrialEligible;
            [JsonProperty("introOffer")] public MockOffer IntroOffer;
            [JsonProperty("retentionOffer")] public MockOffer RetentionOffer;
            [JsonProperty("isSandbox")] public bool IsSandbox = true;
            [JsonProperty("signedRequest")] public string SignedRequest = "mock-signed-request";
        }

        /// <summary>The one mock subscription product id.</summary>
        internal const string MockSubscriptionId = "yes2.mock.vip.monthly";

        // Session-only subscription state, reset on each play.
        internal static bool SubscriptionActive;
        internal static bool RetentionClaimed;

        // Sample catalog returned by GetCatalogAsync. PurchaseAsync accepts
        // ANY product id (not just these) so games can test with their real
        // ids before the platform catalog exists.
        private static readonly MockProduct[] Catalog =
        {
            new MockProduct { ProductId = "yes2.mock.coins.small", Title = "Small Coin Pack", Description = "Mock consumable product.", Price = "$0.99", PriceAmount = 99 },
            new MockProduct { ProductId = "yes2.mock.coins.large", Title = "Large Coin Pack", Description = "Mock consumable product.", Price = "$4.99", PriceAmount = 499 },
            new MockProduct { ProductId = "yes2.mock.noads", Title = "Remove Ads", Description = "Mock non-consumable product.", Price = "$2.99", PriceAmount = 299 },
        };

        // Purchases made this Play Mode session. Intentionally not persisted:
        // each play starts from a clean, predictable state.
        private static readonly List<MockPurchase> Purchases = new List<MockPurchase>();
        private static int _paymentCounter;

        internal static string CatalogJson => JsonConvert.SerializeObject(Catalog);

        internal static string PurchasesJson => JsonConvert.SerializeObject(Purchases);

        internal static MockProduct FindProduct(string productId)
        {
            foreach (var product in Catalog)
            {
                if (product.ProductId == productId) return product;
            }
            return null;
        }

        /// <summary>
        /// Record a confirmed purchase and return its Purchase JSON payload.
        /// </summary>
        internal static string RecordPurchase(string productId, string developerPayload)
        {
            var purchase = new MockPurchase
            {
                PurchaseToken = $"mock-token-{Guid.NewGuid():N}",
                ProductId = productId,
                PaymentId = $"mock-payment-{++_paymentCounter}",
                PurchaseTime = DateTime.UtcNow.ToString("o"),
                DeveloperPayload = string.IsNullOrEmpty(developerPayload) ? null : developerPayload
            };
            Purchases.Add(purchase);
            return JsonConvert.SerializeObject(purchase);
        }

        internal static bool IsKnownSubscription(string productId) => productId == MockSubscriptionId;

        /// <summary>The mock subscription with the current session state.</summary>
        internal static MockSubscription CurrentSubscription()
        {
            return new MockSubscription
            {
                ProductId = MockSubscriptionId,
                Title = "VIP Monthly",
                Description = "Mock monthly subscription.",
                Price = "4.99 USD",
                PriceAmount = 4.99,
                BillingPeriod = "monthly",
                IsActive = SubscriptionActive,
                // Trial and intro pricing only apply before the first subscription.
                TrialEligible = !SubscriptionActive,
                IntroOffer = SubscriptionActive ? null : new MockOffer { PriceAmount = 0.99, DurationPeriods = 1 },
                RetentionOffer = RetentionClaimed ? null : new MockOffer { PriceAmount = 1.99, DurationPeriods = 3 }
            };
        }

        /// <summary>JSON array returned by GetSubscriptionsAsync for a registered player.</summary>
        internal static string SubscriptionsJson => JsonConvert.SerializeObject(new[] { CurrentSubscription() });

        /// <summary>Mark the subscription active and return the subscribed result JSON.</summary>
        internal static string Subscribe()
        {
            SubscriptionActive = true;
            return JsonConvert.SerializeObject(new { status = "subscribed", subscription = CurrentSubscription() });
        }

        /// <summary>Result JSON for a closed checkout.</summary>
        internal const string CancelledResultJson = "{\"status\":\"cancelled\"}";

        /// <summary>Use up the retention offer and return the refreshed subscription JSON.</summary>
        internal static string ClaimRetentionOffer()
        {
            RetentionClaimed = true;
            return JsonConvert.SerializeObject(CurrentSubscription());
        }

        internal static void Consume(string purchaseToken)
        {
            Purchases.RemoveAll(p => p.PurchaseToken == purchaseToken);
        }

        // Statics survive Play Mode restarts when Domain Reload is disabled
        // (Enter Play Mode Options), so reset explicitly on each play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Purchases.Clear();
            _paymentCounter = 0;
            SubscriptionActive = false;
            RetentionClaimed = false;
        }
    }
}
#endif
