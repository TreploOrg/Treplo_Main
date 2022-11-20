using Discord;
using Discord.WebSocket;
using Treplo.Models;

namespace Treplo;

public interface IPlayerSessionsManager : IDisposable, IAsyncDisposable
{
    ValueTask PlayAsync(ulong guildId, IVoiceChannel? voiceChannel, Track? track = null);
    ValueTask<Guid> StartSearchAsync(ulong guildId, IVoiceChannel voiceChannel, TrackSearchResult[] tracks);
    ValueTask<Track> RespondToSearchAsync(ulong guildId, IVoiceChannel voiceChannel, Guid searchId, int searchResultIndex);

    const string SearchSelectMenuId = "searchSongSelector";
}