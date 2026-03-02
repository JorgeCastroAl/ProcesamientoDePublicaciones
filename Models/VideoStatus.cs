namespace VideoProcessingSystemV2.Models
{
    /// <summary>
    /// Represents the processing status of a video.
    /// </summary>
    public enum VideoStatus
    {
        Pending,
        DownloadingAudio,
        ExtractingComments,
        Transcribing,
        GeneratingResponse,
        Processing,  // Mantener para compatibilidad
        Completed,
        Failed
    }
}
