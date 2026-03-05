using System;
using System.Threading.Tasks;

namespace FluxAnswer.Pipeline.Common
{
    public interface IPipelineManager<TStatistics, TProcessedEventArgs>
        where TProcessedEventArgs : EventArgs
    {
        Task StartAsync();
        Task StopAsync();
        Task<TStatistics> GetStatisticsAsync();
        event EventHandler<TProcessedEventArgs> ItemProcessed;
    }
}
