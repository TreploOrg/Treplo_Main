namespace Treplo.SearchService.Searching;

public interface ISearchEngineManager
{
    IAsyncEnumerable<TrackSearchResult> SearchAsync(string searchQuery, CancellationToken cancellationToken = default);
}