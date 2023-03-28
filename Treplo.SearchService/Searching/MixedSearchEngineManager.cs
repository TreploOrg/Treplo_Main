using System.Runtime.CompilerServices;

namespace Treplo.SearchService.Searching;

public class MixedSearchEngineManager : ISearchEngineManager
{
    private readonly ISearchEngine[] engines;

    public MixedSearchEngineManager(IEnumerable<ISearchEngine> engines)
    {
        this.engines = engines.ToArray();
    }

    public async IAsyncEnumerable<TrackSearchResult> SearchAsync(
        string searchQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
            yield break;

        foreach (var searchEngine in engines)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            await foreach (var result in searchEngine.FindAsync(searchQuery, cancellationToken))
            {
                yield return result;
                if (cancellationToken.IsCancellationRequested)
                    yield break;
            }
        }
    }
}