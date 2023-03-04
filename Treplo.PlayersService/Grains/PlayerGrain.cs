using Treplo.Common;

namespace Treplo.PlayersService.Grains;

public sealed class PlayerGrain : Grain, IPlayerGrain
{
    private readonly Queue<Track> queue = new();
    private readonly ILogger<PlayerGrain> logger;
    private LoopState loopState = LoopState.Off;

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

    public ValueTask<Track[]> GetQueue() => ValueTask.FromResult(queue.ToArray());

    public ValueTask<Track?> Dequeue()
    {
        if (!queue.TryDequeue(out var track)) 
            return ValueTask.FromResult<Track?>(null);
        if(loopState == LoopState.On)
            queue.Enqueue(track);
        return ValueTask.FromResult<Track?>(track);

    }

    public ValueTask<LoopState> SwitchLoop()
    {
        loopState = loopState == LoopState.Off ? LoopState.On : LoopState.Off;

        return ValueTask.FromResult(loopState);
    }

    public ValueTask<Track[]> Shuffle()
    {
        var array = queue.ToArray();
        var n = array.Length;
        for (var i = array.Length - 1; i > 1; i--)
        {
            var k = Random.Shared.Next(n + 1);  
            (array[k], array[n]) = (array[n], array[k]);
        }
        
        foreach (var track in array)
        {
            queue.Enqueue(track);
        }
        
        return ValueTask.FromResult(array);
    }

    public ValueTask<LoopState> GetLoopState() => ValueTask.FromResult(loopState);
}