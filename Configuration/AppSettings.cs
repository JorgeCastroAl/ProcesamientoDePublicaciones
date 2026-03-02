using Newtonsoft.Json;

namespace VideoProcessingSystemV2.Configuration
{
    /// <summary>
    /// Application settings model for JSON deserialization.
    /// </summary>
    public class AppSettings
    {
        [JsonProperty("pocketbase_url")]
        public string? PocketBaseUrl { get; set; }

        [JsonProperty("pocketbase_admin_email")]
        public string? PocketBaseAdminEmail { get; set; }

        [JsonProperty("pocketbase_admin_password")]
        public string? PocketBaseAdminPassword { get; set; }

        [JsonProperty("assemblyai_api_key")]
        public string? AssemblyAIApiKey { get; set; }

        [JsonProperty("response_api_url")]
        public string? ResponseApiUrl { get; set; }

        [JsonProperty("ffmpeg_path")]
        public string? FFmpegPath { get; set; }

        [JsonProperty("extraction_interval_minutes")]
        public int? ExtractionIntervalMinutes { get; set; }

        [JsonProperty("processing_retry_count")]
        public int? ProcessingRetryCount { get; set; }

        [JsonProperty("processing_poll_interval_seconds")]
        public int? ProcessingPollIntervalSeconds { get; set; }

        [JsonProperty("temp_directory")]
        public string? TempDirectory { get; set; }

        [JsonProperty("comments_extraction_limit")]
        public int? CommentsExtractionLimit { get; set; }

        [JsonProperty("skip_transcription")]
        public bool? SkipTranscription { get; set; }

        [JsonProperty("recreate_database")]
        public bool? RecreateDatabase { get; set; }

        [JsonProperty("seed_data_restore_enabled")]
        public bool? SeedDataRestoreEnabled { get; set; }

        [JsonProperty("seed_data_directory")]
        public string? SeedDataDirectory { get; set; }
    }
}
