using Newtonsoft.Json;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Response model from Opinion Analyzer API.
    /// </summary>
    public class ResponseApiResponse
    {
        [JsonProperty("videoId")]
        public string? VideoId { get; set; }

        [JsonProperty("opinion")]
        public string? Opinion { get; set; }

        [JsonProperty("profileUsed")]
        public string? ProfileUsed { get; set; }

        [JsonProperty("commentsAnalyzed")]
        public int CommentsAnalyzed { get; set; }
    }
}

