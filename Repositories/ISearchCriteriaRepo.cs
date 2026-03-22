using FluxAnswer.Models;

namespace FluxAnswer.Repositories
{
    public interface ISearchCriteriaRepo
    {
        Task<List<SearchCriteria>> GetAllAsync();
        Task<List<SearchCriteria>> GetActiveAsync();
    }
}
