using System;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace VideoProcessingSystemV2.Models
{
    [PocketBaseCollection("comment")]
    public class CommentRecord
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("video_id")]
        [PocketBaseField(FieldType = "relation", Required = true, RelationCollection = "video", CascadeDelete = true)]
        public string VideoId { get; set; } = string.Empty;

        [JsonProperty("comment_id")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string CommentId { get; set; } = string.Empty;

        [JsonProperty("text")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("author")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("like_count")]
        [PocketBaseField(FieldType = "number")]
        public int? LikeCount { get; set; }

        [JsonProperty("timestamp")]
        [PocketBaseField(FieldType = "date")]
        public DateTime? Timestamp { get; set; }

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }

        public CommentRecord()
        {
        }

        public CommentRecord(string videoId, string commentId, string text, string author)
        {
            VideoId = videoId;
            CommentId = commentId;
            Text = text;
            Author = author;
        }
    }
}
