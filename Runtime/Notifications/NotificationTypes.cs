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
    /// <see cref="Title"/> must be set (it may be empty), and exactly one of <see cref="DelaySeconds"/> or
    /// <see cref="ScheduledInDays"/> must be set. Fields left null are not sent.
    /// </summary>
    [Serializable]
    public class NotificationOptions
    {
        /// <summary>
        /// Optional stable id. Scheduling again with the same id replaces the notification on
        /// platforms that support it. Generated when null; an empty string is rejected.
        /// </summary>
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id;

        /// <summary>
        /// Required, but may be empty: the notification is then sent without a title. At most
        /// 200 characters. Null is rejected with InvalidParams.
        /// </summary>
        [JsonProperty("title")]
        public string Title;

        /// <summary>
        /// Body text of the notification (1 to 2000 characters). Sent as an empty string when null,
        /// which platforms that deliver notifications reject with InvalidParams.
        /// </summary>
        [JsonIgnore]
        public string Body;

        /// <summary>
        /// Delay in seconds before the notification is shown (positive, at most 7 days). Set this OR
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
            else if (options.Title == null)
            {
                message = "options.title must be a string";
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

        // Limits of the platforms that deliver notifications. The messages are generic on purpose.
        internal const int MaxTitleLength = 200;
        internal const int MaxBodyLength = 2000;
        internal const int MaxCtaTextLength = 50;
        internal const long MaxDelaySeconds = 7L * 86_400L;
        internal const string DefaultCtaText = "Play";

        internal const string EmptyIdMessage = "Notification id must be a non-empty string when provided";
        internal const string BodyLengthMessage = "Notification body must be 1 to 2000 characters";
        internal const string TitleLengthMessage = "Notification title must be at most 200 characters";
        internal const string CtaLengthMessage = "Notification ctaText must be 1 to 50 characters";
        internal const string ImageFormatMessage = "imageDataUrl must be a PNG, JPEG or WebP base64 data URL with a lowercase prefix";
        internal const string ImageSizeMessage = "imageDataUrl must be at most 2 MiB encoded";
        internal const string DelayTooLongMessage = "Notifications must be scheduled within 7 days";

        private static readonly string[] ImageDataUrlPrefixes =
        {
            "data:image/png;base64,",
            "data:image/jpeg;base64,",
            "data:image/webp;base64,"
        };

        /// <summary>
        /// Checks the limits that platforms delivering notifications apply after
        /// <see cref="TryValidate"/> (and after the registered player check), in the same order,
        /// so the Editor mock fails where a platform build would. Lengths count UTF-16 code units,
        /// as the platform does.
        /// </summary>
        internal static bool TryValidatePlatformLimits(NotificationOptions options, out string message)
        {
            message = null;
            string body = options.Body ?? string.Empty;
            string ctaText = options.CtaText ?? DefaultCtaText;
            if (options.Id != null && options.Id.Length == 0)
            {
                message = EmptyIdMessage;
            }
            else if (body.Length < 1 || body.Length > MaxBodyLength)
            {
                message = BodyLengthMessage;
            }
            else if (options.Title != null && options.Title.Length > MaxTitleLength)
            {
                message = TitleLengthMessage;
            }
            else if (ctaText.Length < 1 || ctaText.Length > MaxCtaTextLength)
            {
                message = CtaLengthMessage;
            }
            else if (options.ImageDataUrl != null && !HasImagePrefix(options.ImageDataUrl))
            {
                message = ImageFormatMessage;
            }
            else if (options.ImageDataUrl != null && options.ImageDataUrl.Length > Yes2SDKImage.MaxDataUrlLength)
            {
                message = ImageSizeMessage;
            }
            else if (options.DelaySeconds.HasValue && options.DelaySeconds.Value > MaxDelaySeconds)
            {
                message = DelayTooLongMessage;
            }
            return message == null;
        }

        private static bool HasImagePrefix(string dataUrl)
        {
            foreach (string prefix in ImageDataUrlPrefixes)
            {
                if (dataUrl.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool IsBlank(string value) => value == null || value.Trim().Length == 0;

        private static string PriorityName(NotificationPriority priority)
        {
            switch (priority)
            {
                case NotificationPriority.Low: return "low";
                case NotificationPriority.High: return "high";
                case NotificationPriority.Critical: return "critical";
                // An out-of-range value cast to the enum falls back to the platform default.
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
