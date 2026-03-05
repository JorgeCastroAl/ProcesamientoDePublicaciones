using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    [CollectionName("social_network")]
    public class SocialNetworkRepo : BaseRepository<SocialNetwork>, ISocialNetworkRepo
    {
        public SocialNetworkRepo(PocketBaseOptions options) : base(options) { }

        public async Task<SocialNetwork?> GetByCodeAsync(string code)
        {
            var results = await GetByFilterAsync($"code='{code}'");
            return results.Count > 0 ? results[0] : null;
        }
    }
}
