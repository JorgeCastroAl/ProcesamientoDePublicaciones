using System;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Join collection between bot accounts and videos.
    /// Explicit many-to-many relation model.
    /// </summary>
    [PocketBaseCollection("bot_account_video")]
    public class BotAccountVideo
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("bot_account_id")]
        [PocketBaseField(FieldType = "relation", Required = true, RelationCollection = "bot_account", CascadeDelete = true)]
        public string BotAccountId { get; set; } = string.Empty;

        [JsonProperty("video_id")]
        [PocketBaseField(FieldType = "relation", Required = true, RelationCollection = "video", CascadeDelete = true)]
        public string VideoId { get; set; } = string.Empty;

        [JsonProperty("custom_comment_text")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? CustomCommentText { get; set; }

        [JsonProperty("comment_sent")]
        [PocketBaseField(FieldType = "bool", Required = false)]
        public bool CommentSent { get; set; } = false;

        [JsonProperty("priority")]
        [PocketBaseField(FieldType = "bool", Required = false)]
        public bool Priority { get; set; } = false;

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }

        public BotAccountVideo()
        {
        }

        public BotAccountVideo(string botAccountId, string videoId, string? customCommentText = null)
        {
            BotAccountId = botAccountId;
            VideoId = videoId;
            CustomCommentText = customCommentText;
        }
    }
}
