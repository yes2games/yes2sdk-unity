using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Delivery weight of a notification on platforms that ration notifications.
    /// The platform default is <see cref="Medium"/>.
    /// </summary>
    public enum NotificationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Options for <see cref="Yes2SDKNotifications.ScheduleAsync(NotificationOptions, Action{ScheduledNotification}, Action{Error})"/>.
    /// <see cref="Title"/> is required, and exactly one of <see cref="DelaySeconds"/> or
    /// <see cref="ScheduledInDays"/> must be set. Fields left null are not sent.
    /// </summary>
    [Serializable]
    public class NotificationOptions
    {
        /// <summary>
        /// Optional stable id. Scheduling again with the same id replaces the notification on
        /// platforms that support it. Generated when null.
        /// </summary>
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id;

        /// <summary>Required. Title of the notification.</summary>
        [JsonProperty("title")]
        public string Title;

        /// <summary>Body text of the notification. Sent as an empty string when null.</summary>
        [JsonIgnore]
        public string Body;

        /// <summary>
        /// Delay in seconds before the notification is shown (positive). Set this OR
        /// <see cref="ScheduledInDays"/>, not both.
        /// </summary>
        [JsonProperty("delaySeconds", NullValueHandling = NullValueHandling.Ignore)]
        public int? DelaySeconds;

        /// <summary>
        /// Fuzzy delivery in whole days from now (0 to 7); the platform picks the best time inside
        /// that day. Set this OR <see cref="DelaySeconds"/>, not both.
        /// </summary>
        [JsonProperty("scheduledInDays", NullValueHandling = NullValueHandling.Ignore)]
        public int? ScheduledInDays;

        /// <summary>
        /// Optional call-to-action label (1 to 50 characters). A platform default is used when null.
        /// </summary>
        [JsonProperty("ctaText", NullValueHandling = NullValueHandling.Ignore)]
        public string CtaText;

        /// <summary>Optional delivery weight. The platform default is Medium.</summary>
        [JsonIgnore]
        public NotificationPriority? Priority;

        /// <summary>
        /// Optional id of a pre-approved image in the platform's image library. Set this OR
        /// <see cref="ImageDataUrl"/>, not both. Ignored on platforms without notification images.
        /// </summary>
        [JsonProperty("imageAssetId", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageAssetId;

        /// <summary>
        /// Optional image rendered at runtime, as a base64 data URL (PNG, JPEG or WebP, at most
        /// 2 MiB encoded, lowercase prefix such as "data:image/png;base64,"). Build one from a
        /// texture with <see cref="Yes2SDKImage.ToPngDataUrl(UnityEngine.Texture2D)"/>. Set this OR
        /// <see cref="ImageAssetId"/>, not both. Ignored on platforms without notification images.
        /// </summary>
        [JsonProperty("imageDataUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageDataUrl;

        /// <summary>Optional URL of the notification icon, on platforms that use one.</summary>
        [JsonProperty("iconUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string IconUrl;

        /// <summary>Optional data handed back to the game when the player opens the notification.</summary>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Data;

        // Body is required by the platform layer, so it is always sent.
        [JsonProperty("body")]
        private string BodyJson => Body ?? string.Empty;

        // Sent as the lowercase name the platform expects ("high").
        [JsonProperty("priority", NullValueHandling = NullValueHandling.Ignore)]
        private string PriorityJson => Priority.HasValue ? PriorityName(Priority.Value) : null;

        /// <summary>The JSON sent to the platform. Unset fields are omitted.</summary>
        internal string ToJson()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        /// <summary>
        /// Checks the options with the same rules and messages the platform layer applies, so the
        /// Editor mock fails where a platform build would. The platform stays the authority at runtime.
        /// </summary>
        internal static bool TryValidate(NotificationOptions options, out string message)
        {
            message = null;
            if (options == null)
            {
                message = "options must be a valid object";
            }
            else if (IsBlank(options.Title))
            {
                message = "options.title must be a non-empty string";
            }
            else if (options.ImageAssetId != null && options.ImageDataUrl != null)
            {
                message = "Provide at most one of options.imageAssetId or options.imageDataUrl";
            }
            else if (options.ImageAssetId != null && IsBlank(options.ImageAssetId))
            {
                message = "options.imageAssetId must be a non-empty string when provided";
            }
            else if (options.ImageDataUrl != null && IsBlank(options.ImageDataUrl))
            {
                message = "options.imageDataUrl must be a non-empty string when provided";
            }
            else if (options.DelaySeconds.HasValue == options.ScheduledInDays.HasValue)
            {
                message = "Provide exactly one of options.delaySeconds or options.scheduledInDays";
            }
            else if (options.DelaySeconds.HasValue && options.DelaySeconds.Value <= 0)
            {
                message = "options.delaySeconds must be a positive number";
            }
            else if (options.ScheduledInDays.HasValue && (options.ScheduledInDays.Value < 0 || options.ScheduledInDays.Value > 7))
            {
                message = "options.scheduledInDays must be an integer from 0 to 7";
            }
            return message == null;
        }

        private static bool IsBlank(string value) => value == null || value.Trim().Length == 0;

        private static string PriorityName(NotificationPriority priority)
        {
            switch (priority)
            {
                case NotificationPriority.Low: return "low";
                case NotificationPriority.High: return "high";
                case NotificationPriority.Critical: return "critical";
                default: return "medium";
            }
        }
    }

    /// <summary>A notification the platform accepted.</summary>
    [Serializable]
    public struct ScheduledNotification
    {
        /// <summary>Notification id. Pass it to <c>CancelAsync</c>.</summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>Title of the notification.</summary>
        [JsonProperty("title")]
        public string Title;

        /// <summary>Body text of the notification.</summary>
        [JsonProperty("body")]
        public string Body;

        /// <summary>When the notification is due, as a Unix timestamp in milliseconds.</summary>
        [JsonProperty("scheduledAt")]
        public long ScheduledAt;
    }
}
