using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Error information returned from Yes2SDK operations.
    /// </summary>
    [Serializable]
    public struct Error
    {
        /// <summary>
        /// Error code string identifier.
        /// </summary>
        [JsonProperty("code")]
        public string Code;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        [JsonProperty("message")]
        public string Message;

        /// <summary>
        /// Context where the error occurred (e.g., "ads.showInterstitial").
        /// </summary>
        [JsonProperty("context")]
        public string Context;

        /// <summary>
        /// Gets the typed error code enum value.
        /// </summary>
        public ErrorCode ErrorCode
        {
            get
            {
                if (string.IsNullOrEmpty(Code))
                {
                    return ErrorCode.Unknown;
                }

                // Codes raised in C# and by the jslib use the enum names
                // ("PlatformError"). Numeric strings are rejected: TryParse would
                // accept "7" as a value that is not a real code.
                if (!char.IsDigit(Code[0]) && Enum.TryParse<ErrorCode>(Code, true, out var result)
                    && Enum.IsDefined(typeof(ErrorCode), result))
                {
                    return result;
                }

                // Errors from the Core runtime reach Unity with Core's own
                // SCREAMING_SNAKE codes ("PLATFORM_ERROR"), which never parse as
                // enum names, so without this map almost every real platform
                // error read as Unknown.
                return CoreCodes.TryGetValue(Code, out var mapped) ? mapped : ErrorCode.Unknown;
            }
        }

        private static readonly Dictionary<string, ErrorCode> CoreCodes = new Dictionary<string, ErrorCode>
        {
            ["NOT_INITIALIZED"] = ErrorCode.NotInitialized,
            ["INITIALIZATION_ERROR"] = ErrorCode.NotInitialized,
            ["INVALID_PARAM"] = ErrorCode.InvalidParams,
            ["INVALID_OPERATION"] = ErrorCode.InvalidParams,
            ["FEATURE_NOT_SUPPORTED"] = ErrorCode.FeatureNotSupported,
            ["PLATFORM_NOT_SUPPORTED"] = ErrorCode.FeatureNotSupported,
            ["CLIENT_UNSUPPORTED"] = ErrorCode.FeatureNotSupported,
            ["IAP_NOT_AVAILABLE"] = ErrorCode.FeatureNotSupported,
            ["PLATFORM_ERROR"] = ErrorCode.PlatformError,
            ["ADS_NOT_LOADED"] = ErrorCode.PlatformError,
            ["ADS_NO_FILL"] = ErrorCode.PlatformError,
            ["ADS_BLOCKED"] = ErrorCode.PlatformError,
            ["IAP_PURCHASE_FAILED"] = ErrorCode.PlatformError,
            ["IAP_ALREADY_PURCHASED"] = ErrorCode.PlatformError,
            ["STORAGE_ERROR"] = ErrorCode.PlatformError,
            ["STORAGE_QUOTA_EXCEEDED"] = ErrorCode.PlatformError,
            ["PLAYER_DATA_CORRUPTED"] = ErrorCode.PlatformError,
            ["PLAYER_NOT_AUTHENTICATED"] = ErrorCode.PlatformError,
            ["LEADERBOARD_NOT_FOUND"] = ErrorCode.PlatformError,
            ["NETWORK_FAILURE"] = ErrorCode.NetworkError,
            ["ADS_FREQUENCY_LIMITED"] = ErrorCode.RateLimited,
            ["TIMEOUT"] = ErrorCode.Timeout,
            ["UNKNOWN_ERROR"] = ErrorCode.Unknown,
        };

        public override string ToString()
        {
            return $"[Error] {Code}: {Message} (context: {Context})";
        }
    }

    /// <summary>
    /// Standard error codes returned by Yes2SDK.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>SDK has not been initialized.</summary>
        NotInitialized,

        /// <summary>Invalid parameters provided to API call.</summary>
        InvalidParams,

        /// <summary>Feature is not supported on the current platform.</summary>
        FeatureNotSupported,

        /// <summary>Platform SDK returned an error.</summary>
        PlatformError,

        /// <summary>Network request failed.</summary>
        NetworkError,

        /// <summary>Operation was rate limited.</summary>
        RateLimited,

        /// <summary>User cancelled the operation.</summary>
        UserCancelled,

        /// <summary>Unknown error occurred.</summary>
        Unknown,

        /// <summary>
        /// The platform did not answer in time. Ads report this when an ad
        /// never starts, or starts and never finishes; the ad is released so
        /// later ads can run.
        /// </summary>
        Timeout
    }
}
