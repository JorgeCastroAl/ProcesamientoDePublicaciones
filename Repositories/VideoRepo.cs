using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
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
            // Un video está incompleto si alguna de estas condiciones es falsa:
            // - audio_downloaded = false
            // - comments_extracted = false  
            // - response_generated = false
            // - (transcription_completed = false Y skip_transcription = false)
            var filter = "audio_downloaded=false || comments_extracted=false || response_generated=false";
            return await GetByFilterAsync(filter);
        }
    }
}
