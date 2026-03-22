using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    [CollectionName("extract_comments")]
    public class ExtractedCommentRepo : BaseRepository<ExtractedCommentRecord>, IExtractedCommentRepo
    {
        public ExtractedCommentRepo(PocketBaseOptions options) : base(options) { }

        public async Task<List<ExtractedCommentRecord>> GetByVideoIdAsync(string videoId) =>
            string.IsNullOrWhiteSpace(videoId)
                ? new List<ExtractedCommentRecord>()
                : await GetByFilterAsync($"video_id='{EscapeFilterLiteral(videoId)}'");

        private static string EscapeFilterLiteral(string value) =>
            value?.Replace("\\", "\\\\").Replace("'", "\\'") ?? string.Empty;
    }
}
