using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace FluxAnswer.Models
{
    [PocketBaseCollection("extract_comments")]
    public class ExtractedCommentRecord
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("video_id")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string VideoId { get; set; } = string.Empty;

        [JsonProperty("comment_external_id")]
        [PocketBaseField(FieldType = "text")]
        public string CommentExternalId { get; set; } = string.Empty;

        [JsonProperty("author")]
        [PocketBaseField(FieldType = "text")]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("text")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("like_count")]
        [PocketBaseField(FieldType = "number")]
        public int LikeCount { get; set; } = 0;

        [JsonProperty("commented_at")]
        [PocketBaseField(FieldType = "date")]
        public DateTime? CommentedAt { get; set; }

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }
    }
}
