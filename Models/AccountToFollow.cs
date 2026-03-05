using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Represents a TikTok account to follow and extract videos from.
    /// </summary>
    [PocketBaseCollection("account_to_follow")]
    public class AccountToFollow
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("legacy_id")]
        [PocketBaseField(FieldType = "number", Required = false)]
        public int? LegacyId { get; set; }

        [JsonProperty("social_network_id")]
        [PocketBaseField(FieldType = "relation", RelationCollection = "social_network", Required = false)]
        public string? SocialNetworkId { get; set; }

        [JsonProperty("username")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("profile_url")]
        [Required]
        [PocketBaseField(FieldType = "url", Required = true)]
        public string ProfileUrl { get; set; } = string.Empty;

        [JsonProperty("description_text")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? Description { get; set; }

        [JsonProperty("profile_type_value")]
        [PocketBaseField(FieldType = "number", Required = false)]
        public int? Type { get; set; }

        [JsonProperty("profile_type_display")]
        [PocketBaseField(FieldType = "text", Required = false, Pattern = "^(PERSONA|PARTIDO|NOTICIERO)?$")]
        public string? TypeDisplay { get; set; }

        [JsonProperty("is_active")]
        [PocketBaseField(FieldType = "bool", Required = false)]
        public bool IsActive { get; set; } = true;

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }

        public AccountToFollow()
        {
        }

        public AccountToFollow(string username, string profileUrl)
        {
            Username = username;
            ProfileUrl = profileUrl;
        }
    }
}

