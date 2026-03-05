using System;

namespace FluxAnswer.Pipeline.Common
{
    public class PipelineStatistics
    {
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime? LastProcessedTime { get; set; }
        public string? LastError { get; set; }
    }
}
