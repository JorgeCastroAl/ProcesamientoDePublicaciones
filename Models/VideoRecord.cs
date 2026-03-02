using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using PocketBase.Framework.Attributes;

namespace VideoProcessingSystemV2.Models
{
    /// <summary>
    /// Represents a TikTok video record in the processing pipeline.
    /// </summary>
    [PocketBaseCollection("video")]
    public class VideoRecord
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        [JsonProperty("tiktok_video_id")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string TiktokVideoId { get; set; } = string.Empty;

        [JsonProperty("account_username")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string AccountUsername { get; set; } = string.Empty;

        [JsonProperty("video_url")]
        [Required]
        [PocketBaseField(FieldType = "url", Required = true)]
        public string VideoUrl { get; set; } = string.Empty;

        [JsonProperty("title")]
        [PocketBaseField(FieldType = "text")]
        public string? Title { get; set; }

        [JsonProperty("author")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("upload_date")]
        [PocketBaseField(FieldType = "date")]
        public DateTime UploadDate { get; set; }

        [JsonProperty("status")]
        [Required]
        [PocketBaseField(FieldType = "text", Required = true)]
        public string Status { get; set; } = "pending";

        [JsonProperty("transcription")]
        [PocketBaseField(FieldType = "editor")]
        public string? Transcription { get; set; }

        [JsonProperty("error_message")]
        [PocketBaseField(FieldType = "text")]
        public string? ErrorMessage { get; set; }

        // Stage completion flags
        [JsonProperty("audio_downloaded")]
        [PocketBaseField(FieldType = "bool")]
        public bool AudioDownloaded { get; set; } = false;

        [JsonProperty("comments_extracted")]
        [PocketBaseField(FieldType = "bool")]
        public bool CommentsExtracted { get; set; } = false;

        [JsonProperty("transcription_completed")]
        [PocketBaseField(FieldType = "bool")]
        public bool TranscriptionCompleted { get; set; } = false;

        [JsonProperty("response_generated")]
        [PocketBaseField(FieldType = "bool")]
        public bool ResponseGenerated { get; set; } = false;

        [JsonProperty("created")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated")]
        public DateTime UpdatedAt { get; set; }

        public VideoRecord()
        {
        }

        public VideoRecord(string tiktokVideoId, string accountUsername, string videoUrl, string author)
        {
            TiktokVideoId = tiktokVideoId;
            AccountUsername = accountUsername;
            VideoUrl = videoUrl;
            Author = author;
            Status = "pending";
        }

        /// <summary>
        /// Gets the VideoStatus enum value from the status string.
        /// </summary>
        public VideoStatus GetStatusEnum()
        {
            return Status.ToLower() switch
            {
                "pending" => VideoStatus.Pending,
                "processing" => VideoStatus.Processing,
                "completed" => VideoStatus.Completed,
                "failed" => VideoStatus.Failed,
                _ => VideoStatus.Pending
            };
        }

        /// <summary>
        /// Sets the status from a VideoStatus enum value.
        /// </summary>
        public void SetStatus(VideoStatus status)
        {
            Status = status.ToString().ToLower();
        }
    }
}
