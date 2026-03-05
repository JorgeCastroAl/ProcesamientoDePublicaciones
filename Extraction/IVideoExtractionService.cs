using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Service for extracting video metadata from TikTok accounts.
    /// </summary>
    public interface IVideoExtractionService
    {
        Task<ExtractionResult> ExtractVideosAsync(AccountToFollow account);
    }
}

