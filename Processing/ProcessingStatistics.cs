using System;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Statistics for the processing pipeline.
    /// </summary>
    public class ProcessingStatistics
    {
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime? LastProcessedTime { get; set; }
        public string? LastError { get; set; }
    }
}
