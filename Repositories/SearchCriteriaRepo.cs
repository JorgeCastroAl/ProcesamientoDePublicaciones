using PocketBase.Framework;
using PocketBase.Framework.Repository;
using PocketBase.Framework.Attributes;
using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    [CollectionName("search_criteria")]
    public class SearchCriteriaRepo : BaseRepository<SearchCriteria>, ISearchCriteriaRepo
    {
        public SearchCriteriaRepo(PocketBaseOptions options) : base(options) { }

        public async Task<List<SearchCriteria>> GetActiveAsync()
        {
            return await GetByFilterAsync("is_active=true");
        }
    }
}
