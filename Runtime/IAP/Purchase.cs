using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// A completed purchase, as returned by <c>PurchaseAsync</c> and <c>GetPurchasesAsync</c>.
    /// Parse the JSON those calls deliver with <see cref="FromJson"/> or <see cref="ListFromJson"/>.
    /// </summary>
    [Serializable]
    public class Purchase
    {
        /// <summary>Product that was purchased.</summary>
        [JsonProperty("productId")]
        public string ProductId;

        /// <summary>Unique purchase token. Pass it to <c>ConsumePurchaseAsync</c>.</summary>
        [JsonProperty("purchaseToken")]
        public string PurchaseToken;

        /// <summary>Payment identifier from the platform.</summary>
        [JsonProperty("paymentId")]
        public string PaymentId;

        /// <summary>ISO 8601 timestamp of the purchase.</summary>
        [JsonProperty("purchaseTime")]
        public string PurchaseTime;

        /// <summary>Developer payload passed during purchase, or null.</summary>
        [JsonProperty("developerPayload")]
        public string DeveloperPayload;

        /// <summary>
        /// Signed request for server verification, or null when the platform does not provide one.
        /// Verify it on your server before granting value.
        /// </summary>
        [JsonProperty("signedRequest")]
        public string SignedRequest;

        /// <summary>
        /// True when no real money changed hands (sandbox tester or platform simulator).
        /// Grant the item as usual, but keep it out of revenue reporting. False when absent.
        /// </summary>
        [JsonProperty("isSandbox")]
        public bool IsSandbox;

        /// <summary>
        /// Parse a single purchase. Returns null and logs a warning when the input is not valid.
        /// </summary>
        public static Purchase FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Yes2Log.Warning("Purchase.FromJson: empty input");
                return null;
            }
            try
            {
                var purchase = JsonConvert.DeserializeObject<Purchase>(json);
                if (purchase == null) Yes2Log.Warning("Purchase.FromJson: input did not contain a purchase");
                return purchase;
            }
            catch (Exception ex)
            {
                Yes2Log.Warning($"Purchase.FromJson: could not parse purchase ({ex.Message})");
                return null;
            }
        }

        /// <summary>
        /// Parse a JSON array of purchases. Returns an empty list and logs a warning when the input is not valid.
        /// </summary>
        public static List<Purchase> ListFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Yes2Log.Warning("Purchase.ListFromJson: empty input");
                return new List<Purchase>();
            }
            try
            {
                return JsonConvert.DeserializeObject<List<Purchase>>(json) ?? new List<Purchase>();
            }
            catch (Exception ex)
            {
                Yes2Log.Warning($"Purchase.ListFromJson: could not parse purchases ({ex.Message})");
                return new List<Purchase>();
            }
        }

        public override string ToString()
        {
            return $"[Purchase] ProductId={ProductId}, PaymentId={PaymentId}, IsSandbox={IsSandbox}";
        }
    }
}
