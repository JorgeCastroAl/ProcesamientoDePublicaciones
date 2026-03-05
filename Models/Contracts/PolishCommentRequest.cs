using Newtonsoft.Json;

namespace FluxAnswer.Models
{
    public class PolishCommentRequest
    {
        [JsonProperty("affinityLevel")]
        public string AffinityLevel { get; set; } = "IMPARCIAL";

        [JsonProperty("moodState")]
        public string MoodState { get; set; } = "ALEGRE";

        [JsonProperty("personalityType")]
        public string PersonalityType { get; set; } = "AMABLE";

        [JsonProperty("gender")]
        public string Gender { get; set; } = "NEUTRO";

        [JsonProperty("generatedComment")]
        public string GeneratedComment { get; set; } = string.Empty;
    }
}
