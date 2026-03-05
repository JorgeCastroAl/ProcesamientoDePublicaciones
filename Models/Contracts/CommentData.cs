using System;
using Newtonsoft.Json;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Represents comment data extracted from yt-dlp.
    /// </summary>
    public class CommentData
    {
        [JsonProperty("id")]
        public string CommentId { get; set; } = string.Empty;

        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("author")]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("like_count")]
        public int? LikeCount { get; set; }

        [JsonProperty("timestamp")]
        public long? Timestamp { get; set; }

        /// <summary>
        /// Gets the timestamp as DateTime.
        /// </summary>
        public DateTime GetTimestamp()
        {
            if (Timestamp.HasValue)
            {
                return DateTimeOffset.FromUnixTimeSeconds(Timestamp.Value).DateTime;
            }
            return DateTime.UtcNow;
        }
    }
}

