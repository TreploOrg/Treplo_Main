using Treplo.Models;

namespace Treplo;

public interface ISearchClient
{
    IAsyncEnumerable<Track> FindAsync(string query, CancellationToken cancellationToken = default);
}