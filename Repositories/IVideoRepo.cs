using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
{
    public interface IVideoRepo : IRepository<VideoRecord>
    {
        Task<List<VideoRecord>> GetPendingVideosAsync();
        Task<List<VideoRecord>> GetByAccountUsernameAsync(string accountUsername);
        Task<bool> ExistsByTikTokIdAsync(string tiktokVideoId);
        Task<List<VideoRecord>> GetByStatusAsync(string status);
        Task<List<VideoRecord>> GetIncompleteVideosAsync();
    }
}
