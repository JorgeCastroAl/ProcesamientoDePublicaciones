using System;
using System.Threading.Tasks;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Manages the continuous processing pipeline for videos.
    /// </summary>
    public interface IProcessingPipelineManager
    {
        Task StartAsync();
        Task StopAsync();
        Task<ProcessingStatistics> GetStatisticsAsync();
        event EventHandler<VideoProcessedEventArgs> VideoProcessed;
    }
}
