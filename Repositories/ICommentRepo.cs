using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
{
    public interface ICommentRepo : IRepository<CommentRecord>
    {
        Task<List<CommentRecord>> GetByVideoIdAsync(string videoId);
        Task<bool> ExistsByCommentIdAsync(string commentId);
    }
}
