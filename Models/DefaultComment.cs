using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace FluxAnswer.Models
{
    [PocketBaseCollection("default_comments")]
    public class DefaultComment
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("text")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }
    }
}
