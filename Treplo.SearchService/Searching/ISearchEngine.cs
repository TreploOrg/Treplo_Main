using System.Runtime.CompilerServices;
using SimpleResult;
using Treplo.Common;
using Treplo.SearchService.Searching.Errors;

namespace Treplo.SearchService.Searching;

public interface ISearchEngine
{
    string Name { get; }

    async IAsyncEnumerable<Result<TrackSearchResult, Error>> FindAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (var track in FindInternalAsync(query, cancellationToken))
        {
            yield return track.Map(
                static (track, name) => new TrackSearchResult
                {
                    Track = track,
                    SearchEngineName = name,
                },
                Name
            );
        }
    }

    protected IAsyncEnumerable<Result<Track, Error>> FindInternalAsync(
        string query,
        CancellationToken cancellationToken = default
    );
}