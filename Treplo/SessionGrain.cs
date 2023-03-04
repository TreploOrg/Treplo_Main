using System.IO.Pipelines;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Treplo.Common;
using Treplo.Converters;
using Treplo.PlayersService;
using static Treplo.PlayersService.PlayersService;

namespace Treplo;

public sealed class SessionGrain : Grain, ISessionGrain, IAsyncDisposable
{
    private static readonly StreamFormatRequest StreamFormat = new()
    {
        Channels = 2,
        Frequency = 48000,
        Container = new()
        {
            Name = "s16le",
        },
    };

    //TODO: make owned wrapper, for testing and separation
    private readonly DiscordSocketClient discordSocketClient;
    private readonly PlayersServiceClient playerServiceClient;
    private readonly FfmpegFactory ffmpegFactory;
    private readonly ILogger<SessionGrain> logger;
    private readonly Dictionary<Guid, Track[]> activeSearches = new();

    private CancellationTokenSource? cts = new();
    private Task? playbackTask;
    private ulong? currentVoiceChannel;

    public SessionGrain(
        DiscordSocketClient discordSocketClient,
        PlayersServiceClient playerServiceClient,
        FfmpegFactory ffmpegFactory,
        ILogger<SessionGrain> logger
    )
    {
        this.discordSocketClient = discordSocketClient;
        this.playerServiceClient = playerServiceClient;
        this.ffmpegFactory = ffmpegFactory;
        this.logger = logger;
    }

    public ValueTask StartPlay(ulong voiceChannelId)
    {
        if (currentVoiceChannel == voiceChannelId)
            return ValueTask.CompletedTask;

        //TODO: need to do something then connected to another voice channel
        if (discordSocketClient.GetChannel(voiceChannelId) is not IVoiceChannel channel)
            throw new Exception();
        playbackTask = StartPlaybackCore(channel, cts.Token);
        currentVoiceChannel = voiceChannelId;
        return ValueTask.CompletedTask;
    }

    private async Task StartPlaybackCore(IAudioChannel voiceChannel, CancellationToken cancellationToken)
    {
        // TODO: find a way to handle disconnects gracefully and to cause disconnects
        using var audioClient = await voiceChannel.ConnectAsync(selfDeaf: true);

        audioClient.Disconnected += OnAudioClientOnDisconnected;
        try
        {
            await using var audioOutStream = audioClient.CreatePCMStream(AudioApplication.Music);
            var dequeueRequest = new DequeueRequest
            {
                PlayerRequest = new()
                {
                    PlayerId = this.GetPrimaryKeyString(),
                },
            };
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await playerServiceClient.DequeueAsync(
                    dequeueRequest,
                    cancellationToken: cancellationToken
                );
                var track = result.Track;
                if (track is null)
                    break;
                var audioInStream =
                    playerServiceClient.Play(
                        new()
                        {
                            AudioSource = track.Source,
                        },
                        cancellationToken: cancellationToken
                    ).ResponseStream.ReadAllAsync(cancellationToken: cancellationToken);

                var ffmpeg = ffmpegFactory.Create(track.Source, in StreamFormat);

                var ffmpegInputTask = StartInPipe(ffmpeg.Input, audioInStream, cancellationToken);
                var ffmpegOutTask = ffmpeg.Output.CopyToAsync(audioOutStream, cancellationToken);

                await Task.WhenAll(ffmpegInputTask, ffmpegOutTask, ffmpeg.StartAsync(cancellationToken));
                await audioOutStream.FlushAsync(cancellationToken);

                //TODO: probably need settings for this
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during playback in session {SessionId}", this.GetPrimaryKeyString());
        }
        finally
        {
            await audioClient.StopAsync();
            await voiceChannel.DisconnectAsync();
            await OnAudioClientOnDisconnected(null!);
        }

        static async Task StartInPipe(
            PipeWriter ffmpegInput,
            IAsyncEnumerable<AudioFrame> audioStream,
            CancellationToken cancellationToken
        )
        {
            await foreach (var frame in audioStream.WithCancellation(cancellationToken))
            {
                var result = await ffmpegInput.WriteAsync(frame.Bytes.Memory, cancellationToken);
                if (result.IsCompleted)
                    return;
                if (result.IsCanceled || frame.IsEnd)
                    break;
            }

            await ffmpegInput.CompleteAsync();
        }

        Task OnAudioClientOnDisconnected(Exception _)
        {
            FireCts();
            currentVoiceChannel = null;
            return Task.CompletedTask;
        }
    }

    private void FireCts(bool recreate = true)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = recreate ? new CancellationTokenSource() : null;
    }

    public ValueTask<Guid> StartSearch(Track[] searchTracks)
    {
        var searchId = Guid.NewGuid();
        activeSearches[searchId] = searchTracks;
        return ValueTask.FromResult(searchId);
    }

    public async ValueTask<Track> EndSearch(Guid searchSessionId, uint trackId)
    {
        if (!activeSearches.Remove(searchSessionId, out var requests))
            throw new ArgumentException("Unknown session id", nameof(searchSessionId));
        logger.LogInformation("Responding to search {SearchId}", searchSessionId);
        var track = requests[trackId];
        await Enqueue(track);
        return track;
    }

    public async ValueTask Enqueue(Track track)
    {
        await playerServiceClient.EnqueueAsync(
            new()
            {
                PlayerRequest = new()
                {
                    PlayerId = this.GetPrimaryKeyString(),
                },
                Track = track,
            }
        );
    }

    public async ValueTask Pause()
    {
        FireCts();
        if (playbackTask is not null)
            await playbackTask;
        playbackTask = null;
        currentVoiceChannel = null;
    }

    public async ValueTask DisposeAsync()
    {
        FireCts(false);

        if (playbackTask is null)
            return;

        await playbackTask;
        playbackTask = null;
    }
}