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

        /// <summary>
        /// Optional slug of an onboarding game that invited players are routed through first.
        /// Ignored on platforms without onboarding games. An empty string is rejected.
        /// </summary>
        [JsonProperty("onboardingSlug", NullValueHandling = NullValueHandling.Ignore)]
        public string OnboardingSlug;

        /// <summary>
        /// Optional notifications sent to the referrer as invited players join. The platform uses
        /// the template with the highest <see cref="ReferralNotificationTemplate.MinConversionCount"/>
        /// the referrer has reached and picks one of its variants. Ignored on platforms without
        /// referral notifications.
        /// </summary>
        [JsonProperty("notificationTemplates", NullValueHandling = NullValueHandling.Ignore)]
        public List<ReferralNotificationTemplate> NotificationTemplates;

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

        /// <summary>
        /// Checks <see cref="OnboardingSlug"/> and <see cref="NotificationTemplates"/> with the same
        /// rules and messages the platform layer applies, so the Editor mock fails where a platform
        /// build would. <see cref="Reference"/> is checked by the caller.
        /// </summary>
        internal static bool TryValidateOptionalFields(ReferralShareOptions options, out string message)
        {
            message = null;
            if (options.OnboardingSlug != null && IsBlank(options.OnboardingSlug))
            {
                message = "options.onboardingSlug must be a non-empty string";
                return false;
            }
            if (options.NotificationTemplates == null) return true;

            for (int i = 0; i < options.NotificationTemplates.Count; i++)
            {
                string at = $"options.notificationTemplates[{i}]";
                ReferralNotificationTemplate template = options.NotificationTemplates[i];
                if (template == null)
                {
                    message = $"{at} must be an object";
                    return false;
                }
                if (template.MinConversionCount < 0)
                {
                    message = $"{at}.minConversionCount must be a non-negative integer";
                    return false;
                }
                if (template.Variants == null || template.Variants.Count == 0)
                {
                    message = $"{at}.variants must be a non-empty array";
                    return false;
                }
                for (int j = 0; j < template.Variants.Count; j++)
                {
                    string vat = $"{at}.variants[{j}]";
                    ReferralNotificationVariant variant = template.Variants[j];
                    if (variant == null)
                    {
                        message = $"{vat} must be an object";
                        return false;
                    }
                    if (IsBlank(variant.Body) || IsBlank(variant.CtaText))
                    {
                        message = $"{vat}.body and {vat}.ctaText must be non-empty strings";
                        return false;
                    }
                    if (variant.ImageReference != null && IsBlank(variant.ImageReference))
                    {
                        message = $"{vat}.imageReference must be a non-empty string or null";
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsBlank(string value) => value == null || value.Trim().Length == 0;
    }

    /// <summary>
    /// Referral conversion notifications that apply once the referrer reaches
    /// <see cref="MinConversionCount"/> conversions.
    /// </summary>
    [Serializable]
    public class ReferralNotificationTemplate
    {
        /// <summary>Conversions the referrer needs before this template applies (0 or more).</summary>
        [JsonProperty("minConversionCount")]
        public int MinConversionCount;

        /// <summary>Wordings the platform picks from. At least one is required.</summary>
        [JsonProperty("variants")]
        public List<ReferralNotificationVariant> Variants = new List<ReferralNotificationVariant>();
    }

    /// <summary>One wording of a referral conversion notification.</summary>
    [Serializable]
    public class ReferralNotificationVariant
    {
        /// <summary>Optional notification title.</summary>
        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title;

        /// <summary>Required. Notification body text.</summary>
        [JsonProperty("body")]
        public string Body;

        /// <summary>Required. Call-to-action label.</summary>
        [JsonProperty("ctaText")]
        public string CtaText;

        /// <summary>Optional id of a pre-approved image in the platform's image library.</summary>
        [JsonProperty("imageReference", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageReference;
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
