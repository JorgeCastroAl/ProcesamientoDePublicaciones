using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    public interface IBotAccountRepo : IRepository<BotAccount>
    {
        Task<List<BotAccount>> GetActiveAccountsAsync();
        Task<BotAccount?> GetByUsernameAsync(string username);
    }
}

