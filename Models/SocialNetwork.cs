using System;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace FluxAnswer.Models
{
    [PocketBaseCollection("social_network")]
    public class SocialNetwork
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("code")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("name")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("is_active")]
        [PocketBaseField(FieldType = "bool", Required = false)]
        public bool IsActive { get; set; } = true;

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }
    }
}
