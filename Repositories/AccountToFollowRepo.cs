using System.Linq;
using System.Threading.Tasks;
using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Repositories
{
    [CollectionName("account_to_follow")]
    public class AccountToFollowRepo : BaseRepository<AccountToFollow>, IAccountToFollowRepo
    {
        public AccountToFollowRepo(PocketBaseOptions options) : base(options) { }

        public async Task<AccountToFollow?> GetByUsernameAsync(string username)
        {
            var results = await GetByFilterAsync($"username='{username}'");
            return results.FirstOrDefault();
        }
    }
}
