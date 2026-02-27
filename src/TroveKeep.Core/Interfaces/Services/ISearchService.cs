using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ISearchService
{
    Task<SearchResult> SearchAsync(string query);
}
