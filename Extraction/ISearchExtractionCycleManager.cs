using System;
using System.Threading.Tasks;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Manages the TikTokApi-based search extraction cycle.
    /// </summary>
    public interface ISearchExtractionCycleManager
    {
        DateTime? LastExtractionTime { get; }
        Task StartAsync();
        Task StopAsync();
        event EventHandler<ExtractionCompletedEventArgs> ExtractionCompleted;
    }
}
