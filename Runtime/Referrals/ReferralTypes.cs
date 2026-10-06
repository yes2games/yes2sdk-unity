using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Options for <see cref="Yes2SDKReferrals.ShareAsync(ReferralShareOptions, Action{ReferralShareResult}, Action{Error})"/>.
    /// Only <see cref="Reference"/> is required; fields left null are not sent.
    /// </summary>
    [Serializable]
    public class ReferralShareOptions
    {
        /// <summary>
        /// Required. Stable campaign key that groups the conversions, for example "unlock_party_mode_v1".
        /// </summary>
        [JsonProperty("reference")]
        public string Reference;

        /// <summary>
        /// Optional data delivered to the invited player. They read it with
        /// <c>Yes2SDK.Session.GetEntryPointData()</c>.
        /// </summary>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Data;

        /// <summary>Optional title for the share dialog.</summary>
        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title;

        /// <summary>Optional message for the share dialog.</summary>
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text;

        /// <summary>
        /// Optional image as a base64 data URL (PNG, JPEG or WebP), at most 2 MiB,
        /// for example "data:image/png;base64,...".
        /// </summary>
        [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageDataUrl;

        /// <summary>Creates empty options; set the fields you need.</summary>
        public ReferralShareOptions()
        {
        }

        /// <summary>Creates options with the given reference.</summary>
        public ReferralShareOptions(string reference)
        {
            Reference = reference;
        }

        /// <summary>The JSON sent to the platform. Unset fields are omitted.</summary>
        internal string ToJson()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }
    }

    /// <summary>Result of a referral share.</summary>
    [Serializable]
    public class ReferralShareResult
    {
        /// <summary>True when the player closed the share dialog without sharing.</summary>
        [JsonProperty("canceled")]
        public bool Canceled;
    }

    /// <summary>A player who joined through a referral link.</summary>
    [Serializable]
    public class ReferralConversion
    {
        /// <summary>The player who joined.</summary>
        [JsonProperty("playerId")]
        public string PlayerId;

        /// <summary>When they joined, as an ISO 8601 timestamp.</summary>
        [JsonProperty("joinedAt")]
        public string JoinedAt;
    }

    /// <summary>The current player's referral conversions, grouped by reference.</summary>
    [Serializable]
    public class ReferralList
    {
        /// <summary>
        /// Conversions keyed by the <see cref="ReferralShareOptions.Reference"/> they came from.
        /// Never null; empty when nobody has joined yet.
        /// </summary>
        [JsonProperty("referrals")]
        public Dictionary<string, List<ReferralConversion>> Referrals = new Dictionary<string, List<ReferralConversion>>();

        /// <summary>
        /// Platform-signed payload. Verify it on your server before granting a referral reward.
        /// </summary>
        [JsonProperty("signedRequest")]
        public string SignedRequest;
    }
}
