using System.Threading.Tasks;
using PocketBase.Framework.Repository;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    public interface ISocialNetworkRepo : IRepository<SocialNetwork>
    {
        Task<SocialNetwork?> GetByCodeAsync(string code);
    }
}
