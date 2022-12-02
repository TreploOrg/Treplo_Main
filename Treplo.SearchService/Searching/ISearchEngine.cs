using System.Runtime.CompilerServices;
using Treplo.Common.Models;

namespace Treplo.SearchService.Searching;

public interface ISearchEngine
{
    async IAsyncEnumerable<TrackSearchResult> FindAsync(string query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var track in FindInternalAsync(query, cancellationToken))
        {
            yield return new TrackSearchResult(track, Name);
        }
    }

    protected IAsyncEnumerable<Track> FindInternalAsync(string query, CancellationToken cancellationToken = default);
    
    string Name { get; }
}