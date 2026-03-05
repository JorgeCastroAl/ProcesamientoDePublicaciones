using System;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Join collection between bot accounts and accounts to follow.
    /// Stores only relation IDs as recommended for explicit many-to-many modeling.
    /// </summary>
    [PocketBaseCollection("bot_account_account_to_follow")]
    public class BotAccountAccountToFollow
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("bot_account_id")]
        [PocketBaseField(FieldType = "relation", Required = true, RelationCollection = "bot_account", CascadeDelete = true)]
        public string BotAccountId { get; set; } = string.Empty;

        [JsonProperty("account_to_follow_id")]
        [PocketBaseField(FieldType = "relation", Required = true, RelationCollection = "account_to_follow", CascadeDelete = true)]
        public string AccountToFollowId { get; set; } = string.Empty;

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }

        public BotAccountAccountToFollow()
        {
        }

        public BotAccountAccountToFollow(string botAccountId, string accountToFollowId)
        {
            BotAccountId = botAccountId;
            AccountToFollowId = accountToFollowId;
        }
    }
}

