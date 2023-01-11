using Treplo.Common.Models;

namespace Treplo.PlayersService.Interfaces;

public interface IPlayerGrain : IGrainWithStringKey
{
    ValueTask Enqueue(Track track);
    ValueTask<Track[]> ShowQueue();
    ValueTask<Guid> StartSearch(Track[] searchTracks);
    ValueTask<Track> RespondToSearch(Guid searchSessionId, uint searchResultIndex);
    ValueTask<Track?> Dequeue();
}