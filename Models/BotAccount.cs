using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace VideoProcessingSystemV2.Models
{
    /// <summary>
    /// Represents a bot account used for posting responses.
    /// </summary>
    [PocketBaseCollection("bot_account")]
    public class BotAccount
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("legacy_id")]
        [PocketBaseField(FieldType = "number", Required = false)]
        public int? LegacyId { get; set; }

        [JsonProperty("username")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("credentials")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Credentials { get; set; } = string.Empty;

        [JsonProperty("password")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? Password { get; set; }

        [JsonProperty("description")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? Description { get; set; }

        [JsonProperty("affinity_level")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? AffinityLevel { get; set; }

        [JsonProperty("mood_state")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? MoodState { get; set; }

        [JsonProperty("personality_type")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? PersonalityType { get; set; }

        [JsonProperty("gender")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? Gender { get; set; }

        [JsonProperty("is_active")]
        [PocketBaseField(FieldType = "bool", Required = false)]
        public bool IsActive { get; set; } = true;

        [JsonProperty("accounts_to_follow_ids")]
        [PocketBaseField(FieldType = "relation", RelationCollection = "account_to_follow", MaxSelect = -1, Required = false)]
        public List<string> AccountsToFollowIds { get; set; } = new List<string>();

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }

        public BotAccount()
        {
        }

        public BotAccount(string username, string credentials)
        {
            Username = username;
            Credentials = credentials;
        }
    }
}
