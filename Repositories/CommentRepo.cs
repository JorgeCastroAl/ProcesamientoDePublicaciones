using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
{
    [CollectionName("comment")]
    public class CommentRepo : BaseRepository<CommentRecord>, ICommentRepo
    {
        public CommentRepo(PocketBaseOptions options) : base(options) { }

        public async Task<List<CommentRecord>> GetByVideoIdAsync(string videoId) =>
            await GetByFilterAsync($"video_id='{videoId}'");

        public async Task<bool> ExistsByCommentIdAsync(string commentId) =>
            await ExistsByFilterAsync($"comment_id='{commentId}'");
    }
}
