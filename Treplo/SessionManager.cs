using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Serilog;
using Treplo.Clients;
using Treplo.Common.Models;

namespace Treplo;

public sealed class SessionManager
{
    private readonly PlayerServiceClient playerServiceClient;
    private readonly ConcurrentDictionary<ulong, PlaybackSession> sessions = new();

    public SessionManager(PlayerServiceClient playerServiceClient)
    {
        this.playerServiceClient = playerServiceClient;
    }

    public async ValueTask StartPlayBackAsync(ulong guildId, IVoiceChannel voiceChannel)
    {
        var session = GetSession(guildId);
        await session.TryAttachAsync(voiceChannel, id => playerServiceClient.PlayAsync(id));
    }

    private PlaybackSession GetSession(ulong id)
    {
        return sessions.GetOrAdd(id, static id => new PlaybackSession(id));
    }

    public async Task RespondToSearchAsync(ulong guildId, Guid searchId, uint index)
    {
        await playerServiceClient.RespondToSearchAsync(guildId, searchId, index);
    }

    public async Task EnqueueAsync(ulong guildId, TrackRequest track)
    {
        await playerServiceClient.EnqueueAsync(guildId, track);
    }

    public async Task<Guid> StartSearchAsync(ulong guildId, TrackRequest[] trackRequests)
    {
        return await playerServiceClient.StartSearchAsync(guildId, trackRequests);
    }
}

public sealed class PlaybackSession
{
    private readonly ulong id;
    private IVoiceChannel? currentVoiceChannel;

    private Task? playbackTask;

    public PlaybackSession(ulong id)
    {
        this.id = id;
    }

    public async ValueTask TryAttachAsync(IVoiceChannel voiceChannel, Func<ulong, Task<Stream>> streamFactory)
    {
        if (currentVoiceChannel?.Id == voiceChannel.Id && playbackTask is not null)
            return;

        currentVoiceChannel = voiceChannel;
        
        playbackTask = Core();


        async Task Core()
        {
            try
            {
                await using var currentStream = await streamFactory(id);
                using var client = await voiceChannel.ConnectAsync();
                await using var opusStream = client.CreatePCMStream(AudioApplication.Music);
                await currentStream.CopyToAsync(opusStream);
            }
            catch (Exception e)
            {
                Log.Error(e, "error in session");
            }
            finally
            {
                playbackTask = null;
            }
        }
    }
}