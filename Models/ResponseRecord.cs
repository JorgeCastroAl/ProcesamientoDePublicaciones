using System;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace VideoProcessingSystemV2.Models
{
    [PocketBaseCollection("response")]
    public class ResponseRecord
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("video_id")]
        [PocketBaseField(FieldType = "relation", Required = true, RelationCollection = "video", CascadeDelete = true)]
        public string VideoId { get; set; } = string.Empty;

        [JsonProperty("response_text")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string ResponseText { get; set; } = string.Empty;

        [JsonProperty("api_status")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string ApiStatus { get; set; } = "success";

        [JsonProperty("posted")]
        [PocketBaseField(FieldType = "bool")]
        public bool Posted { get; set; } = false;

        [JsonProperty("posted_at")]
        [PocketBaseField(FieldType = "date")]
        public DateTime PostedAt { get; set; }

        // PocketBase manages these automatically - don't send them in POST
        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        [JsonIgnore] // Ignore when serializing
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated", NullValueHandling = NullValueHandling.Ignore)]
        [JsonIgnore] // Ignore when serializing
        public DateTime? UpdatedAt { get; set; }
    }
}
