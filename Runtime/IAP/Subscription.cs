using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yes2SDK
{
    /// <summary>
    /// A subscription offer plus the current player's entitlement for it, as
    /// returned by <c>Yes2SDK.IAP.GetSubscriptionsAsync</c>, <c>SubscribeAsync</c>
    /// and <c>ClaimRetentionOfferAsync</c>.
    /// </summary>
    [Serializable]
    public class Subscription
    {
        /// <summary>Subscription product identifier.</summary>
        [JsonProperty("productId")]
        public string ProductId;

        /// <summary>Display title.</summary>
        [JsonProperty("title")]
        public string Title;

        /// <summary>Description. Empty when the platform has none.</summary>
        [JsonProperty("description")]
        public string Description;

        /// <summary>Formatted price, for example "4.99 USD".</summary>
        [JsonProperty("price")]
        public string Price;

        /// <summary>Price per billing period as a number.</summary>
        [JsonProperty("priceAmount")]
        public double PriceAmount;

        /// <summary>ISO 4217 currency code, for example "USD".</summary>
        [JsonProperty("priceCurrencyCode")]
        public string PriceCurrencyCode;

        /// <summary>Billing period: "weekly", "monthly" or "yearly".</summary>
        [JsonProperty("billingPeriod")]
        public string BillingPeriod;

        /// <summary>True when the player holds this subscription. Grant the entitlement when true.</summary>
        [JsonProperty("isActive")]
        public bool IsActive;

        /// <summary>True when the player can start a free trial. Show trial copy only when true.</summary>
        [JsonProperty("trialEligible")]
        public bool TrialEligible;

        /// <summary>Introductory offer, or null when there is none.</summary>
        [JsonProperty("introOffer")]
        public SubscriptionOffer IntroOffer;

        /// <summary>One-time retention discount, or null when there is none.</summary>
        [JsonProperty("retentionOffer")]
        public SubscriptionOffer RetentionOffer;

        /// <summary>
        /// True when no real money changed hands (sandbox tester or platform simulator).
        /// Grant as usual, but keep it out of revenue reporting. False when absent.
        /// </summary>
        [JsonProperty("isSandbox")]
        public bool IsSandbox;

        /// <summary>
        /// Signed payload for server verification, or null when the platform does not provide one.
        /// Verify it on your server before granting anything of value.
        /// </summary>
        [JsonProperty("signedRequest")]
        public string SignedRequest;

        /// <summary>Parses one subscription. Throws when the input is not a subscription object.</summary>
        internal static Subscription Parse(string json)
        {
            var subscription = string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Subscription>(json);
            if (subscription == null) throw new FormatException("payload is not a subscription");
            return subscription;
        }

        /// <summary>Parses a JSON array of subscriptions. Throws when the input is not an array.</summary>
        internal static List<Subscription> ParseList(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new FormatException("payload is not a subscription list");
            return JsonConvert.DeserializeObject<List<Subscription>>(json) ?? new List<Subscription>();
        }

        public override string ToString()
        {
            return $"[Subscription] ProductId={ProductId}, BillingPeriod={BillingPeriod}, IsActive={IsActive}, IsSandbox={IsSandbox}";
        }
    }

    /// <summary>A discounted price for a number of billing periods.</summary>
    [Serializable]
    public class SubscriptionOffer
    {
        /// <summary>Discounted price per billing period.</summary>
        [JsonProperty("priceAmount")]
        public double PriceAmount;

        /// <summary>Number of billing periods the discount lasts.</summary>
        [JsonProperty("durationPeriods")]
        public int DurationPeriods;
    }

    /// <summary>Outcome of a subscription checkout.</summary>
    public enum SubscribeStatus
    {
        /// <summary>The player subscribed.</summary>
        Subscribed,

        /// <summary>The player closed the checkout. This is not an error.</summary>
        Cancelled
    }

    /// <summary>
    /// Result of <c>Yes2SDK.IAP.SubscribeAsync</c>. A closed checkout is a
    /// result with <see cref="SubscribeStatus.Cancelled"/>, not an error.
    /// </summary>
    public class SubscribeResult
    {
        /// <summary>Whether the player subscribed or closed the checkout.</summary>
        public SubscribeStatus Status;

        /// <summary>The subscription when <see cref="Status"/> is Subscribed, otherwise null.</summary>
        public Subscription Subscription;

        /// <summary>True when the player subscribed.</summary>
        public bool IsSubscribed => Status == SubscribeStatus.Subscribed;

        /// <summary>
        /// Parses <c>{"status":"subscribed","subscription":{...}}</c> or
        /// <c>{"status":"cancelled"}</c>. Throws on any other shape.
        /// </summary>
        internal static SubscribeResult Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new FormatException("payload is not a subscribe result");
            var root = JToken.Parse(json) as JObject;
            if (root == null) throw new FormatException("payload is not a subscribe result");

            string status = (string)root["status"];
            switch (status)
            {
                case "subscribed":
                    var subscription = root["subscription"] as JObject;
                    if (subscription == null) throw new FormatException("subscribed result has no subscription");
                    return new SubscribeResult
                    {
                        Status = SubscribeStatus.Subscribed,
                        Subscription = subscription.ToObject<Subscription>()
                    };
                case "cancelled":
                    return new SubscribeResult { Status = SubscribeStatus.Cancelled };
                default:
                    throw new FormatException($"unknown subscribe status '{status}'");
            }
        }

        public override string ToString()
        {
            return $"[SubscribeResult] Status={Status}, ProductId={Subscription?.ProductId}";
        }
    }
}
