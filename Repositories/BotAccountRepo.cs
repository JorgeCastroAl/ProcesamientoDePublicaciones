using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    [CollectionName("bot_account")]
    public class BotAccountRepo : BaseRepository<BotAccount>, IBotAccountRepo
    {
        public BotAccountRepo(PocketBaseOptions options) : base(options) { }

        public async Task<List<BotAccount>> GetActiveAccountsAsync() =>
            await GetByFilterAsync("is_active=true");

        public async Task<BotAccount?> GetByUsernameAsync(string username)
        {
            var results = await GetByFilterAsync($"username='{username}'");
            return results.FirstOrDefault();
        }
    }
}

