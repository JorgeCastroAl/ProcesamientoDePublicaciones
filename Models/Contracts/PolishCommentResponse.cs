using Newtonsoft.Json;

namespace FluxAnswer.Models
{
    public class PolishCommentResponse
    {
        [JsonProperty("originalComment")]
        public string? OriginalComment { get; set; }

        [JsonProperty("polishedComment")]
        public string? PolishedComment { get; set; }

        [JsonProperty("affinityLevel")]
        public string? AffinityLevel { get; set; }

        [JsonProperty("moodState")]
        public string? MoodState { get; set; }

        [JsonProperty("personalityType")]
        public string? PersonalityType { get; set; }

        [JsonProperty("gender")]
        public string? Gender { get; set; }
    }
}
