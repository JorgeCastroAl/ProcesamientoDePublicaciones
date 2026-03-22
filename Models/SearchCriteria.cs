using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace FluxAnswer.Models
{
    [PocketBaseCollection("search_criteria")]
    public class SearchCriteria
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("keyword")]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Keyword { get; set; } = string.Empty;

        [JsonProperty("is_active")]
        [PocketBaseField(FieldType = "bool")]
        public bool IsActive { get; set; } = true;

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }
    }
}
