using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    public interface IExtractedCommentRepo : IRepository<ExtractedCommentRecord>
    {
        Task<List<ExtractedCommentRecord>> GetByVideoIdAsync(string videoId);
    }
}
