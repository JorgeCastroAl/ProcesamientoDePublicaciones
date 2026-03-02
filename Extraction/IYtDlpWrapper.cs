using System.Collections.Generic;
using System.Threading.Tasks;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Extraction
{
    /// <summary>
    /// Interface for yt-dlp wrapper.
    /// </summary>
    public interface IYtDlpWrapper
    {
        Task<List<VideoMetadata>> ExtractVideosAsync(string url, int count);
        
        Task<List<CommentData>> ExtractCommentsAsync(string videoUrl);
    }
}
