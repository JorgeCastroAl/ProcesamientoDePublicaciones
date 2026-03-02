using System.Collections.Generic;
using Newtonsoft.Json;

namespace VideoProcessingSystemV2.Models
{
    /// <summary>
    /// Request model for Opinion Analyzer API.
    /// </summary>
    public class ResponseApiRequest
    {
        [JsonProperty("videoId")]
        public string? VideoId { get; set; }

        [JsonProperty("comments")]
        public List<string>? Comments { get; set; }

        [JsonProperty("transcription")]
        public string? Transcription { get; set; }

        [JsonProperty("tituloVideo")]
        public string? TituloVideo { get; set; }

        [JsonProperty("profileType")]
        public string ProfileType { get; set; } = "DEFENSA_PERSONA";
    }
}
