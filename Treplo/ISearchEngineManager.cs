using Treplo.Models;

namespace Treplo;

public interface ISearchEngineManager
{
    IAsyncEnumerable<TrackSearchResult> SearchAsync(string searchQuery, CancellationToken cancellationToken = default);
}