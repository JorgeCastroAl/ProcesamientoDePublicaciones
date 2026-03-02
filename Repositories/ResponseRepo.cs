using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
{
    [CollectionName("response")]
    public class ResponseRepo : BaseRepository<ResponseRecord>, IResponseRepo
    {
        public ResponseRepo(PocketBaseOptions options) : base(options) { }

        public async Task<List<ResponseRecord>> GetByVideoIdAsync(string videoId) =>
            await GetByFilterAsync($"video_id='{videoId}'");

        public async Task<List<ResponseRecord>> GetUnpostedAsync() =>
            await GetByFilterAsync("posted=false");

        public async Task<ResponseRecord> MarkAsPostedAsync(string id)
        {
            var entity = await GetByIdAsync(id);
            entity.Posted = true;
            entity.PostedAt = DateTime.UtcNow;
            return await UpdateAsync(id, entity);
        }
    }
}
