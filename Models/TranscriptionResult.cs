using Newtonsoft.Json;

namespace VideoProcessingSystemV2.Models
{
    /// <summary>
    /// Result of a transcription operation.
    /// </summary>
    public class TranscriptionResult
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        public bool IsCompleted => Status == "completed";
        public bool IsError => Status == "error";
        public bool IsProcessing => Status == "processing" || Status == "queued";
    }
}
