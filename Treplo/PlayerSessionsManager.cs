using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Serilog;
using Treplo.Models;
using Treplo.Players;

namespace Treplo;

public sealed class PlayerSessionsManager : IPlayerSessionsManager
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ConcurrentDictionary<ulong, PlayerGuildSession> sessions;
    private readonly ILogger logger;

    public PlayerSessionsManager(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger.ForContext<PlayerSessionsManager>();
        sessions = new ConcurrentDictionary<ulong, PlayerGuildSession>();
    }

    public async ValueTask PlayAsync(ulong guildId, IVoiceChannel? voiceChannel = null, Track? track = null)
    {
        var session = await GetSessionAsync(guildId, voiceChannel);
        var player = session.Player;
        if (track is not null)
            await player.EnqueueAsync(track.GetValueOrDefault());

        await player.StartAsync();
    }

    public async ValueTask<Guid> StartSearchAsync(ulong guildId, IVoiceChannel voiceChannel, TrackSearchResult[] tracks)
    {
        var session = await GetSessionAsync(guildId, voiceChannel);
        var searchId = Guid.NewGuid();
        session.AddSearch(searchId, tracks);
        return searchId;
    }

    public async ValueTask<Track> RespondToSearchAsync(ulong guildId, IVoiceChannel voiceChannel, Guid searchId, int searchResultIndex)
    {
        var session = await GetSessionAsync(guildId);
        logger.Information("Responding to search {SearchId}", searchId);
        var searchResult = session.RespondToSearch(searchId, searchResultIndex);
        logger.Information("Responded to search {SearchId} with resul {@SearchResult}", searchId, searchResult);
        await PlayAsync(guildId, voiceChannel, searchResult.Track);
        return searchResult.Track;
    }

    private async ValueTask<PlayerGuildSession> GetSessionAsync(ulong guildId, IVoiceChannel? voiceChannel = null)
    {
        var session = sessions.GetOrAdd(guildId, CreatSession);
        if (voiceChannel is not null && session.VoiceChannelId != voiceChannel.Id)
            await session.ReattachAsync(voiceChannel);

        return session;
    }

    private PlayerGuildSession CreatSession(ulong arg)
    {
        return new PlayerGuildSession(new NonPersistentPlayer(httpClientFactory, logger));
    }

    private class PlayerGuildSession : IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<Guid, TrackSearchResult[]> searches = new();

        public PlayerGuildSession(IPlayer player)
        {
            Player = player;
        }

        public IPlayer Player { get; }
        public ulong? VoiceChannelId { get; set; }

        public async ValueTask ReattachAsync(IVoiceChannel newVoiceChannel)
        {
            await Player.AttachAsync(newVoiceChannel);
            VoiceChannelId = newVoiceChannel.Id;
        }

        public void AddSearch(Guid searchId, TrackSearchResult[] results)
        {
            searches.TryAdd(searchId, results);
        }

        public TrackSearchResult RespondToSearch(Guid searchId, int searchResultIndex)
        {
            if(searches.TryRemove(searchId, out var searchResults))
                return searchResults[searchResultIndex];
            return default;
        }

        public void Dispose()
        {
            Player.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await Player.DisposeAsync();
        }
    }

    public void Dispose()
    {
        foreach (var (_, session) in sessions)
        {
            session.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, session) in sessions)
        {
            await session.DisposeAsync();
        }
    }
}