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
            await GetByFilterAsync($"account_username='{accountUsername}'");

        public async Task<bool> ExistsByTikTokIdAsync(string tiktokVideoId) =>
            await ExistsByFilterAsync($"tiktok_video_id='{tiktokVideoId}'");

        public async Task<List<VideoRecord>> GetByStatusAsync(string status) =>
            await GetByFilterAsync($"status='{status}'");

        public async Task<List<VideoRecord>> GetIncompleteVideosAsync()
        {
            // Filtra videos que NO tienen todas las etapas completadas
            // Un video estÃ¡ incompleto si alguna de estas condiciones es falsa:
            // - audio_downloaded = false
            // - comments_extracted = false  
            // - response_generated = false
            // - (transcription_completed = false Y skip_transcription = false)
            var filter = "audio_downloaded=false || comments_extracted=false || response_generated=false";
            return await GetByFilterAsync(filter);
        }

        public async Task<VideoRecord?> GetNextIncompleteVideoAsync()
        {
            var filter = "audio_downloaded=false || comments_extracted=false || response_generated=false";
            var encodedFilter = Uri.EscapeDataString(filter);
            var url = $"/api/collections/{_collectionName}/records?filter={encodedFilter}&sort=-priority,updated,created&perPage=1&page=1";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PocketBaseListResponse<VideoRecord>>(content);
            return result?.Items?.FirstOrDefault();
        }
    }
}

