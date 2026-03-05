using System;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace TikTokManager.Models
{
    [PocketBaseCollection("bot_account_session")]
    public class BotAccountSession
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("bot_account_id")]
        [PocketBaseField(FieldType = "relation", RelationCollection = "bot_account", Required = true)]
        public string BotAccountId { get; set; } = string.Empty;

        [JsonProperty("social_network_id")]
        [PocketBaseField(FieldType = "relation", RelationCollection = "social_network", Required = false)]
        public string? SocialNetworkId { get; set; }

        [JsonProperty("user_data_folder")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string UserDataFolder { get; set; } = string.Empty;

        [JsonProperty("webview_profile_name")]
        [PocketBaseField(FieldType = "text", Required = false)]
        public string? WebViewProfileName { get; set; }

        [JsonProperty("last_seen_at")]
        [PocketBaseField(FieldType = "date", Required = false)]
        public DateTime? LastSeenAt { get; set; }

        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? UpdatedAt { get; set; }
    }
}
