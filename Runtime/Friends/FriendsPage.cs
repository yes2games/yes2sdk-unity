using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Paginated result of friends list.
    /// </summary>
    [Serializable]
    public struct FriendsPage
    {
        /// <summary>
        /// List of friends in this page.
        /// </summary>
        [JsonProperty("friends")]
        public List<FriendInfo> Friends;

        /// <summary>
        /// Current page index (0-based).
        /// </summary>
        [JsonProperty("page")]
        public int Page;

        /// <summary>
        /// Page size.
        /// </summary>
        [JsonProperty("size")]
        public int Size;

        /// <summary>
        /// Whether there are more pages available.
        /// </summary>
        [JsonProperty("hasMore")]
        public bool HasMore;

        /// <summary>
        /// Total number of friends.
        /// </summary>
        [JsonProperty("total")]
        public int Total;

        public override string ToString()
        {
            return $"[FriendsPage] Page={Page}, Size={Size}, Total={Total}, HasMore={HasMore}";
        }
    }
}
