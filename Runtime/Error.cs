using System;
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
                if (Enum.TryParse<ErrorCode>(Code, true, out var result))
                {
                    return result;
                }
                return ErrorCode.Unknown;
            }
        }

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
        Unknown
    }
}
