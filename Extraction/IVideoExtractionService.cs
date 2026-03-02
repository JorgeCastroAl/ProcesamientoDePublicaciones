using System.Threading.Tasks;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Extraction
{
    /// <summary>
    /// Service for extracting video metadata from TikTok accounts.
    /// </summary>
    public interface IVideoExtractionService
    {
        Task<ExtractionResult> ExtractVideosAsync(AccountToFollow account);
    }
}
