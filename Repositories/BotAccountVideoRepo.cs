using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    [CollectionName("bot_account_video")]
    public class BotAccountVideoRepo : BaseRepository<BotAccountVideo>, IBotAccountVideoRepo
    {
        public BotAccountVideoRepo(PocketBaseOptions options) : base(options) { }

        public async Task<List<BotAccountVideo>> GetByBotAccountIdAsync(string botAccountId) =>
            await GetByFilterAsync($"bot_account_id='{botAccountId}'");

        public async Task<List<BotAccountVideo>> GetByVideoIdAsync(string videoId) =>
            await GetByFilterAsync($"video_id='{videoId}'");

        public async Task<bool> ExistsLinkAsync(string botAccountId, string videoId) =>
            await ExistsByFilterAsync($"bot_account_id='{botAccountId}' && video_id='{videoId}'");
    }
}
