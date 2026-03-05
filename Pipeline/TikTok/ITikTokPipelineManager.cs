using System;
using System.Threading.Tasks;
using FluxAnswer.Pipeline.Common;

namespace FluxAnswer.Pipeline.TikTok
{
    /// <summary>
    /// Manages the continuous processing pipeline for videos.
    /// </summary>
    public interface ITikTokPipelineManager : IPipelineManager<TikTokPipelineStatistics, TikTokVideoProcessedEventArgs>
    {
        event EventHandler<TikTokVideoProcessedEventArgs> VideoProcessed;
    }
}



