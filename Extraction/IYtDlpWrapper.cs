using System.Collections.Generic;
using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Extraction
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

