using System.Collections.Generic;
using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    public interface IAccountToFollowRepo : IRepository<AccountToFollow>
    {
        Task<AccountToFollow?> GetByUsernameAsync(string username);
    }
}

