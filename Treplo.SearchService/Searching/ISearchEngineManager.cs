using SimpleResult;
using Treplo.SearchService.Searching.Errors;

namespace Treplo.SearchService.Searching;

public interface ISearchEngineManager
{
    IAsyncEnumerable<Result<TrackSearchResult, Error>> SearchAsync(
        string searchQuery,
        CancellationToken cancellationToken = default
    );
}