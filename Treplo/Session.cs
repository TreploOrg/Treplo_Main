using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Treplo.Clients;
using Treplo.Common.Models;
using Treplo.PlayersService.Interfaces;

namespace Treplo;

public sealed class Session : IDisposable
{
    private static readonly StreamFormatRequest StreamFormat = new()
    {
        Channels = 2,
        Frequency = 48000,
        Container = new Container("s16le"),
    };

    private readonly string id;
    private readonly IClusterClient clusterClient;

    //TODO: make owned wrapper, for testing and separation
    private readonly DiscordSocketClient discordSocketClient;
    private readonly PlayerServiceClient playerServiceClient;
    private readonly ILogger<Session> logger;

    private CancellationTokenSource cts = new();
    private Task? playbackTask;
    private ulong? currentVoiceChannel;

    public Session(
        ulong id,
        IClusterClient clusterClient,
        DiscordSocketClient discordSocketClient,
        PlayerServiceClient playerServiceClient,
        ILogger<Session> logger
    )
    {
        this.id = id.ToString();
        this.clusterClient = clusterClient;
        this.discordSocketClient = discordSocketClient;
        this.playerServiceClient = playerServiceClient;
        this.logger = logger;
    }

    public ValueTask StartPlay(ulong voiceChannelId)
    {
        if (currentVoiceChannel == voiceChannelId)
            return ValueTask.CompletedTask;
        
        //TODO: need to do something then connected to another voice channel
        var player = clusterClient.GetGrain<IPlayerGrain>(id);
        if (discordSocketClient.GetChannel(voiceChannelId) is not IVoiceChannel channel)
            throw new Exception();
        playbackTask = StartPlayback(player, channel, cts.Token);
        currentVoiceChannel = voiceChannelId;
        return ValueTask.CompletedTask;
        
        async Task StartPlayback(IPlayerGrain playerGrain, IAudioChannel voiceChannel, CancellationToken cancellationToken)
        {
            using var audioClient = await voiceChannel.ConnectAsync(selfDeaf: true);
            var audioOutStream = audioClient.CreatePCMStream(AudioApplication.Music, (int?)StreamFormat.Frequency);
            audioClient.Disconnected += async _ =>
            {
                await audioOutStream.DisposeAsync();
                currentVoiceChannel = null;
            };
            try
            {
                while (!cancellationToken.IsCancellationRequested && await playerGrain.Dequeue() is { } track)
                {
                    await using var audioInStream =
                        await playerServiceClient.GetAudioStream(track.Source, StreamFormat, cancellationToken);
                    await audioInStream.CopyToAsync(audioOutStream, cancellationToken);

                    //TODO: probably need settings for this
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error during playback in session {SessionId}", id);
            }
            finally
            {
                await audioOutStream.FlushAsync(cancellationToken);
                await audioClient.StopAsync();
            }
        }
    }
    
    public async ValueTask<Guid> StartSearch(Track[] searchTracks)
    {
        var player = clusterClient.GetGrain<IPlayerGrain>(id);
        return await player.StartSearch(searchTracks);
    }

    public async ValueTask<Track> EndSearch(Guid searchSessionId, uint trackId)
    {
        var player = clusterClient.GetGrain<IPlayerGrain>(id);
        return await player.RespondToSearch(searchSessionId, trackId);
    }

    public async ValueTask Enqueue(Track track)
    {
        var player = clusterClient.GetGrain<IPlayerGrain>(id);
        await player.Enqueue(track);
    }

    public async ValueTask Pause()
    {
        cts.Cancel();
        if(playbackTask is not null)
            await playbackTask;
        playbackTask = null;
        currentVoiceChannel = null;
        cts.Dispose();
        cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
        
        playbackTask = null;
    }
}