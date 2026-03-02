using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
{
    public interface IResponseRepo : IRepository<ResponseRecord>
    {
        Task<List<ResponseRecord>> GetByVideoIdAsync(string videoId);
        Task<List<ResponseRecord>> GetUnpostedAsync();
        Task<ResponseRecord> MarkAsPostedAsync(string id);
    }
}
