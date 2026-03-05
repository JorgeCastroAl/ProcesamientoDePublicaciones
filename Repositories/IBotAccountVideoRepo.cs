using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    public interface IBotAccountVideoRepo : IRepository<BotAccountVideo>
    {
        Task<List<BotAccountVideo>> GetByBotAccountIdAsync(string botAccountId);
        Task<List<BotAccountVideo>> GetByVideoIdAsync(string videoId);
        Task<bool> ExistsLinkAsync(string botAccountId, string videoId);
    }
}
