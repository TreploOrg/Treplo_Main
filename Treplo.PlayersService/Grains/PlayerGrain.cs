using Treplo.Common.Models;
using Treplo.PlayersService.Interfaces;

namespace Treplo.PlayersService.Grains;

public sealed class PlayerGrain : Grain, IPlayerGrain
{
    private readonly Queue<Track> queue = new();

    //TODO: Search sessions probably need to be expirable
    private readonly Dictionary<Guid, Track[]> searchSessions = new();
    private readonly ILogger<PlayerGrain> logger;

    public PlayerGrain(ILogger<PlayerGrain> logger)
    {
        this.logger = logger;
    }

    public ValueTask Enqueue(Track track)
    {
        queue.Enqueue(track);
        logger.LogInformation("Enqueued {Track}", track);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Track[]> ShowQueue() => ValueTask.FromResult(queue.ToArray());

    public ValueTask<Guid> StartSearch(Track[] searchTracks)
    {
        var guid = Guid.NewGuid();
        logger.LogInformation("Started search with id {SearchId}, with {TrackCount} tracks", guid, searchTracks.Length);
        searchSessions[guid] = searchTracks;
        return ValueTask.FromResult(guid);
    }

    public async ValueTask<Track> RespondToSearch(Guid searchSessionId, uint searchResultIndex)
    {
        if (!searchSessions.Remove(searchSessionId, out var requests))
            throw new ArgumentException("Unknown session id", nameof(searchSessionId));
        logger.LogInformation("Responding to search {SearchId}", searchSessionId);
        var track = requests[searchResultIndex];
        await Enqueue(track);
        return track;
    }

    public ValueTask<Track?> Dequeue()
    {
        return queue.TryDequeue(out var result)
            ? ValueTask.FromResult<Track?>(result)
            : ValueTask.FromResult<Track?>(null);
    }
}