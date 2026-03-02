using System;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Event arguments for video processing completion.
    /// </summary>
    public class VideoProcessedEventArgs : EventArgs
    {
        public VideoRecord Video { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ProcessingDuration { get; set; }

        public VideoProcessedEventArgs(VideoRecord video)
        {
            Video = video;
        }
    }
}
