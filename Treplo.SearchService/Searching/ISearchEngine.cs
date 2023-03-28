using System.Runtime.CompilerServices;
using Treplo.Common;

namespace Treplo.SearchService.Searching;

public interface ISearchEngine
{
    string Name { get; }

    async IAsyncEnumerable<TrackSearchResult> FindAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (var track in FindInternalAsync(query, cancellationToken))
        {
            yield return new TrackSearchResult
            {
                Track = track,
                SearchEngineName = Name,
            };
        }
    }

    protected IAsyncEnumerable<Track> FindInternalAsync(string query, CancellationToken cancellationToken = default);
}