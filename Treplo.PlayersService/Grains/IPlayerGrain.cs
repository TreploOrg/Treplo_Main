using Treplo.Common;

namespace Treplo.PlayersService.Grains;

public interface IPlayerGrain : IGrainWithStringKey
{
    ValueTask Enqueue(Track track);
    ValueTask<Track[]> GetQueue();
    ValueTask<Track?> Dequeue();
    ValueTask<LoopState> SwitchLoop();
    ValueTask<Track[]> Shuffle();
    ValueTask<LoopState> GetLoopState();
}