using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketBase.Framework.Attributes;
using System.Runtime.Serialization;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Represents a TikTok account to follow and extract videos from.
    /// </summary>
    [PocketBaseCollection("account_to_follow")]
    public class AccountToFollow
    {
        [JsonExtensionData]
        public IDictionary<string, JToken>? ExtraFields { get; set; }

        [JsonProperty("account_username")]
        public string? LegacyAccountUsername { get; set; }

        [JsonProperty("username_raw")]
        public string? LegacyUsernameRaw { get; set; }

        [JsonProperty("url")]
        public string? LegacyUrl { get; set; }

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

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext _)
        {
            // Backward compatibility: hydrate from legacy payload keys when needed.
            if (string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(LegacyAccountUsername))
            {
                Username = LegacyAccountUsername.Trim();
            }

            if (string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(LegacyUsernameRaw))
            {
                Username = LegacyUsernameRaw.Trim();
            }

            if (string.IsNullOrWhiteSpace(Username) && TryGetExtraString("account_username", out var legacyUsername))
            {
                Username = legacyUsername;
            }

            if (string.IsNullOrWhiteSpace(Username) && TryGetExtraString("username_raw", out var legacyUsernameRaw))
            {
                Username = legacyUsernameRaw;
            }

            if (string.IsNullOrWhiteSpace(ProfileUrl) && !string.IsNullOrWhiteSpace(LegacyUrl))
            {
                ProfileUrl = LegacyUrl.Trim();
            }

            if (string.IsNullOrWhiteSpace(ProfileUrl) && TryGetExtraString("url", out var legacyUrl))
            {
                ProfileUrl = legacyUrl;
            }

            Username = Username?.Trim() ?? string.Empty;
            ProfileUrl = ProfileUrl?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(ProfileUrl))
            {
                var marker = "/@";
                var markerIndex = ProfileUrl.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex >= 0)
                {
                    var start = markerIndex + marker.Length;
                    var tail = ProfileUrl[start..];
                    var endIndex = tail.IndexOfAny(new[] { '/', '?', '&' });
                    Username = (endIndex >= 0 ? tail[..endIndex] : tail).Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(ProfileUrl) && !string.IsNullOrWhiteSpace(Username))
            {
                var cleanUsername = Username.Trim().TrimStart('@');
                if (!string.IsNullOrWhiteSpace(cleanUsername))
                {
                    ProfileUrl = $"https://www.tiktok.com/@{cleanUsername}";
                }
            }
        }

        private bool TryGetExtraString(string key, out string value)
        {
            value = string.Empty;

            if (ExtraFields == null || !ExtraFields.TryGetValue(key, out var token))
            {
                return false;
            }

            var candidate = token?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            value = candidate;
            return true;
        }
    }
}

