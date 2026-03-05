using System;
using System.Threading.Tasks;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Manages the extraction cycle that runs periodically to extract videos from accounts.
    /// </summary>
    public interface IExtractionCycleManager
    {
        DateTime? LastExtractionTime { get; }
        Task StartAsync();
        Task StopAsync();
        event EventHandler<ExtractionCompletedEventArgs> ExtractionCompleted;
    }
}

