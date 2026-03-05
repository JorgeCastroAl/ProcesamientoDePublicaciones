using System;
using Newtonsoft.Json;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Represents video metadata extracted from yt-dlp.
    /// </summary>
    public class VideoMetadata
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("uploader")]
        public string Uploader { get; set; } = string.Empty;

        [JsonProperty("uploader_id")]
        public string? UploaderId { get; set; }

        [JsonProperty("uploader_url")]
        public string? UploaderUrl { get; set; }

        [JsonProperty("webpage_url")]
        public string WebpageUrl { get; set; } = string.Empty;

        [JsonProperty("upload_date")]
        public string? UploadDate { get; set; }

        [JsonProperty("timestamp")]
        public long? Timestamp { get; set; }

        [JsonProperty("duration")]
        public int? Duration { get; set; }

        [JsonProperty("view_count")]
        public long? ViewCount { get; set; }

        [JsonProperty("like_count")]
        public long? LikeCount { get; set; }

        [JsonProperty("comment_count")]
        public long? CommentCount { get; set; }

        /// <summary>
        /// Parses the upload_date string (YYYYMMDD) to DateTime.
        /// </summary>
        public DateTime? GetUploadDateTime()
        {
            if (string.IsNullOrEmpty(UploadDate) || UploadDate.Length != 8)
            {
                return Timestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(Timestamp.Value).DateTime : null;
            }

            if (DateTime.TryParseExact(UploadDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }

            return null;
        }
    }
}

