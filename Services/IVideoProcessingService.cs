using System;
using System.Threading.Tasks;

namespace VideoProcessingSystemV2.Services
{
    /// <summary>
    /// Main service interface for video processing system.
    /// </summary>
    public interface IVideoProcessingService
    {
        ServiceState State { get; }
        Task StartAsync();
        Task StopAsync();
        Task<ServiceStatistics> GetStatisticsAsync();
        event EventHandler<ServiceStateChangedEventArgs> StateChanged;
        event EventHandler<ErrorEventArgs> ErrorOccurred;
    }

    public class ServiceStatistics
    {
        public DateTime? LastExtractionTime { get; set; }
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public string? LastError { get; set; }
    }
}
