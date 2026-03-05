using FluxAnswer.Pipeline.Common;

namespace FluxAnswer.Pipeline.YouTube
{
    public interface IYouTubePipelineManager : IPipelineManager<YouTubePipelineStatistics, YouTubeVideoProcessedEventArgs>
    {
    }
}
