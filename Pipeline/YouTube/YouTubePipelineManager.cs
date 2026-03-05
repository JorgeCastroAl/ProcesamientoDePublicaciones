using System;
using System.Threading.Tasks;

namespace FluxAnswer.Pipeline.YouTube
{
    public class YouTubePipelineManager : IYouTubePipelineManager
    {
        private bool _isRunning;

        public event EventHandler<YouTubeVideoProcessedEventArgs>? ItemProcessed;

        protected virtual void OnItemProcessed(YouTubeVideoProcessedEventArgs args)
        {
            ItemProcessed?.Invoke(this, args);
        }

        public Task StartAsync()
        {
            _isRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _isRunning = false;
            return Task.CompletedTask;
        }

        public Task<YouTubePipelineStatistics> GetStatisticsAsync()
        {
            var stats = new YouTubePipelineStatistics
            {
                LastError = _isRunning ? null : "YouTube pipeline is not running"
            };

            return Task.FromResult(stats);
        }
    }
}
