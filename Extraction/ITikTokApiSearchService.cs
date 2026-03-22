using FluxAnswer.Models;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Search-based video extraction using TikTokApi (David Teather).
    /// Single method: pass username, keywords, and max age — get back videos.
    /// </summary>
    public interface ITikTokApiSearchService
    {
        /// <summary>
        /// Searches TikTok for videos matching any of the given keywords,
        /// filtered by account username and max age in days.
        /// </summary>
        /// <param name="username">TikTok username to filter results by author</param>
        /// <param name="keywords">List of search keywords (each searched independently, results merged)</param>
        /// <param name="maxAgeDays">Maximum video age in days</param>
        /// <param name="countPerKeyword">Max videos to collect per keyword</param>
        /// <returns>Deduplicated list of video metadata</returns>
        Task<List<VideoMetadata>> SearchAsync(string username, List<string> keywords, int maxAgeDays, int countPerKeyword = 10);
    }
}
