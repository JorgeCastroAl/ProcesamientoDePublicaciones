using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using PocketBase.Framework.Models;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    [CollectionName("video")]
    public class VideoRepo : BaseRepository<VideoRecord>, IVideoRepo
    {
        public VideoRepo(PocketBaseOptions options) : base(options) { }

        public async Task<List<VideoRecord>> GetPendingVideosAsync() => 
            await GetByStatusAsync("pending");

        public async Task<List<VideoRecord>> GetByAccountUsernameAsync(string accountUsername) =>
            string.IsNullOrWhiteSpace(accountUsername)
                ? new List<VideoRecord>()
                : await GetByFilterAsync($"account_username='{EscapeFilterLiteral(accountUsername)}'");

        public async Task<bool> ExistsByTikTokIdAsync(string tiktokVideoId) =>
            await ExistsByFilterAsync($"tiktok_video_id='{EscapeFilterLiteral(tiktokVideoId)}'");

        public async Task<List<VideoRecord>> GetByStatusAsync(string status) =>
            await GetByFilterAsync($"status='{EscapeFilterLiteral(status)}'");

        public async Task<List<VideoRecord>> GetIncompleteVideosAsync()
        {
            var filter = "audio_downloaded=false || comments_extracted=false || response_generated=false";
            try
            {
                return await GetByFilterAsync(filter);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "GetIncompleteVideosAsync filter query failed. Falling back to in-memory filtering.");
                var all = await GetAllAsync();
                return all.Where(IsVideoIncomplete).ToList();
            }
        }

        public async Task<VideoRecord?> GetNextIncompleteVideoAsync()
        {
            var filter = "audio_downloaded=false || comments_extracted=false || response_generated=false";
            var encodedFilter = Uri.EscapeDataString(filter);
            var url = $"/api/collections/{_collectionName}/records?filter={encodedFilter}&sort=-priority,updated,created&perPage=1&page=1";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PocketBaseListResponse<VideoRecord>>(content);
                var directMatch = result?.Items?.FirstOrDefault();
                if (directMatch != null)
                {
                    return directMatch;
                }
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Serilog.Log.Warning(
                    "GetNextIncompleteVideoAsync returned {StatusCode}. Filter={Filter}. Response={Response}",
                    (int)response.StatusCode,
                    filter,
                    errorBody
                );
            }

            // Compatibility fallback for legacy or partially migrated schemas.
            var fallbackFilter = "status='pending'";
            var fallbackUrl = $"/api/collections/{_collectionName}/records?filter={Uri.EscapeDataString(fallbackFilter)}&perPage=200&page=1";
            var fallbackResponse = await _httpClient.GetAsync(fallbackUrl);
            if (fallbackResponse.IsSuccessStatusCode)
            {
                var fallbackContent = await fallbackResponse.Content.ReadAsStringAsync();
                var fallbackResult = JsonConvert.DeserializeObject<PocketBaseListResponse<VideoRecord>>(fallbackContent);
                var fallbackMatch = fallbackResult?.Items?
                    .OrderByDescending(v => v.Priority)
                    .ThenBy(v => v.UpdatedAt)
                    .ThenBy(v => v.CreatedAt)
                    .FirstOrDefault();
                if (fallbackMatch != null)
                {
                    Serilog.Log.Information("GetNextIncompleteVideoAsync fallback succeeded using filter: {FallbackFilter}", fallbackFilter);
                    return fallbackMatch;
                }
            }
            else
            {
                var fallbackErrorBody = await fallbackResponse.Content.ReadAsStringAsync();
                Serilog.Log.Warning(
                    "GetNextIncompleteVideoAsync fallback returned {StatusCode}. Filter={Filter}. Response={Response}",
                    (int)fallbackResponse.StatusCode,
                    fallbackFilter,
                    fallbackErrorBody
                );
            }

            // Final resilience fallback: query without any server-side filter and pick in memory.
            var noFilterUrl = $"/api/collections/{_collectionName}/records?perPage=200&page=1";
            var noFilterResponse = await _httpClient.GetAsync(noFilterUrl);
            if (!noFilterResponse.IsSuccessStatusCode)
            {
                var noFilterErrorBody = await noFilterResponse.Content.ReadAsStringAsync();
                Serilog.Log.Warning(
                    "GetNextIncompleteVideoAsync no-filter fallback returned {StatusCode}. Response={Response}",
                    (int)noFilterResponse.StatusCode,
                    noFilterErrorBody
                );
                return null;
            }

            var noFilterContent = await noFilterResponse.Content.ReadAsStringAsync();
            var noFilterResult = JsonConvert.DeserializeObject<PocketBaseListResponse<VideoRecord>>(noFilterContent);
            var inMemoryMatch = noFilterResult?.Items?
                .Where(IsVideoIncomplete)
                .OrderByDescending(v => v.Priority)
                .ThenBy(v => v.UpdatedAt)
                .ThenBy(v => v.CreatedAt)
                .FirstOrDefault();

            if (inMemoryMatch != null)
            {
                Serilog.Log.Information("GetNextIncompleteVideoAsync in-memory fallback selected video: {VideoId}", inMemoryMatch.Id);
            }

            return inMemoryMatch;
        }

        private static string EscapeFilterLiteral(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private static bool IsVideoIncomplete(VideoRecord? video)
        {
            if (video == null)
            {
                return false;
            }

            // Keep the same criteria used by the server-side filter and include legacy status fallback.
            return !video.AudioDownloaded
                || !video.CommentsExtracted
                || !video.ResponseGenerated
                || string.Equals(video.Status, "pending", StringComparison.OrdinalIgnoreCase);
        }
    }
}

