using System.Collections.Generic;
using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Services.Scraping.TikTok
{
    /// <summary>
    /// Service for extracting comments from TikTok videos.
    /// </summary>
    public interface ICommentsExtractionService
    {
        Task<List<CommentData>> ExtractCommentsAsync(string videoUrl, int limit = 12);
    }
}
