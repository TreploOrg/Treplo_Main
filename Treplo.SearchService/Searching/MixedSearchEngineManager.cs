using System.Runtime.CompilerServices;
using SimpleResult;
using Treplo.SearchService.Searching.Errors;

namespace Treplo.SearchService.Searching;

public class MixedSearchEngineManager : ISearchEngineManager
{
    private readonly ISearchEngine[] engines;

    public MixedSearchEngineManager(IEnumerable<ISearchEngine> engines)
    {
        this.engines = engines.ToArray();
    }

    public async IAsyncEnumerable<Result<TrackSearchResult, Error>> SearchAsync(
        string searchQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        foreach (var searchEngine in engines)
        {
            await foreach (var result in searchEngine.FindAsync(searchQuery, cancellationToken))
            {
                yield return result;
                if (cancellationToken.IsCancellationRequested)
                    yield break;
            }
        }
    }
}