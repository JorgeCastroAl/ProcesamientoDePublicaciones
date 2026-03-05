using System;

namespace FluxAnswer.Pipeline.YouTube
{
    public class YouTubeVideoProcessedEventArgs : EventArgs
    {
        public string VideoId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
    }
}
