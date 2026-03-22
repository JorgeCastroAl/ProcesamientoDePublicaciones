using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    public interface IVideoRepo : IRepository<VideoRecord>
    {
        Task<List<VideoRecord>> GetPendingVideosAsync();
        Task<List<VideoRecord>> GetByAccountUsernameAsync(string accountUsername);
        Task<bool> ExistsByTikTokIdAsync(string tiktokVideoId);
        Task<List<VideoRecord>> GetByStatusAsync(string status);
        Task<List<VideoRecord>> GetIncompleteVideosAsync();
        Task<VideoRecord?> GetNextIncompleteVideoAsync();

        // Queries por etapa del pipeline
        Task<VideoRecord?> GetNextForAudioAsync();
        Task<VideoRecord?> GetNextForCommentsAsync();
        Task<VideoRecord?> GetNextForTranscriptionAsync();
        Task<VideoRecord?> GetNextForResponseAsync();
        Task<VideoRecord?> GetNextForCustomCommentsAsync();
    }
}

