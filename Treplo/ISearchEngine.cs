using System.Runtime.CompilerServices;
using Treplo.Models;

namespace Treplo;

public interface ISearchEngine
{
    IAsyncEnumerable<TrackSearchResult> FindAsync(string query, [EnumeratorCancellation]CancellationToken cancellationToken = default)
    {
        return FindInternalAsync(query, cancellationToken).Select(x => new TrackSearchResult(x, Name));
    }

    protected IAsyncEnumerable<Track> FindInternalAsync(string query, CancellationToken cancellationToken = default);
    
    string Name { get; }
}