using System;
using FluxAnswer.Models;

namespace FluxAnswer.Pipeline.TikTok
{
    /// <summary>
    /// Event arguments for video processing completion.
    /// </summary>
    public class TikTokVideoProcessedEventArgs : EventArgs
    {
        public VideoRecord Video { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ProcessingDuration { get; set; }

        public TikTokVideoProcessedEventArgs(VideoRecord video)
        {
            Video = video;
        }
    }
}



