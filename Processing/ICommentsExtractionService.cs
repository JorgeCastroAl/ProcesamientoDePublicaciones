using System.Collections.Generic;
using System.Threading.Tasks;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Service for extracting comments from TikTok videos.
    /// </summary>
    public interface ICommentsExtractionService
    {
        Task<List<CommentData>> ExtractCommentsAsync(string videoUrl, int limit = 12);
    }
}
