using Discord;
using Treplo.Common.Models;

namespace Treplo.Players;

public interface IPlayer : IDisposable, IAsyncDisposable
{
    ValueTask EnqueueAsync(Track track);
    ValueTask StartAsync();
    ValueTask PauseAsync();
    ValueTask ClearQueueAsync();

    ValueTask AttachAsync(IVoiceChannel newVoiceChannel);
}