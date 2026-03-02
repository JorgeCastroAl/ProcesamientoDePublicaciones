using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
{
    public interface IBotAccountRepo : IRepository<BotAccount>
    {
        Task<List<BotAccount>> GetActiveAccountsAsync();
        Task<BotAccount?> GetByUsernameAsync(string username);
    }
}
