using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Serilog;
using Treplo.Common.Models;

namespace Treplo.Players;

public sealed class NonPersistentPlayer : IPlayer
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger logger;
    private readonly ConcurrentQueue<Track> trackQueue = new();
    private IAudioClient? audioClient;
    private CancellationTokenSource cts;
    private CurrentTrack? currentTrack;
    private bool disposed;
    private Task? playbackTask;
    private IVoiceChannel? voiceChannel;

    public NonPersistentPlayer(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger.ForContext<NonPersistentPlayer>();
        cts = new CancellationTokenSource();
    }

    public PlayerState State
    {
        get
        {
            if (disposed)
                return PlayerState.Dead;
            if (voiceChannel is null)
                return PlayerState.NotAttached;
            var task = playbackTask;
            if (task is null || task.IsCompleted)
                return PlayerState.Stopped;
            return PlayerState.Playing;
        }
    }

    public bool IsQueueEmpty => trackQueue.IsEmpty;

    public ValueTask DisposeAsync()
    {
        disposed = true;
        audioClient?.Dispose();
        cts.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        disposed = true;
        audioClient?.Dispose();
        cts.Dispose();
    }

    public ValueTask EnqueueAsync(Track track)
    {
        trackQueue.Enqueue(track);
        logger.Information("Enqueued track {Track} to player on id {PlayerId}", track.Title, "");
        return ValueTask.CompletedTask;
    }

    public async ValueTask StartAsync()
    {
        var state = State;
        logger.Information("Trying to start player");
        if (state == PlayerState.Dead)
            throw new ObjectDisposedException(nameof(NonPersistentPlayer), "Tried to start dead player");
        if (state == PlayerState.Playing)
        {
            logger.Information("Player is already playing");
            return;
        }

        if (IsQueueEmpty)
        {
            logger.Information("Queue was empty");
            return;
        }

        await EnsureAudioClientAsync();
        playbackTask = await Task.Factory.StartNew(async o =>
        {
            var player = (NonPersistentPlayer)o!;
            await StartQueuePlayback(player);
        }, this, cts.Token);
    }

    public async ValueTask PauseAsync()
    {
        if (playbackTask is null)
            return;
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
        await playbackTask;
    }

    public ValueTask ClearQueueAsync()
    {
        trackQueue.Clear();
        return ValueTask.CompletedTask;
    }

    public async ValueTask AttachAsync(IVoiceChannel newVoiceChannel)
    {
        var oldState = State;
        await PauseAsync();
        KillAudioClient();
        voiceChannel = newVoiceChannel;
        if (oldState == PlayerState.Playing)
            await StartAsync();
    }

    private static async Task StartQueuePlayback(NonPersistentPlayer player)
    {
        if (player.audioClient is null)
        {
            player.logger.Warning("Couldn't start playback - not connected to voice channel");
            return;
        }

        var cts = player.cts;
        var currTrack = player.GetCurrentTrack();
        if (currTrack is null)
        {
            player.logger.Warning("Couldn't start playback - failed to get current track");
            return;
        }

        using var httpClient = player.httpClientFactory.CreateClient(nameof(NonPersistentPlayer));
        await using var pcm = player.audioClient.CreatePCMStream(AudioApplication.Music);
        var ffmpeg = Ffmpeg.Create().To(pcm);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var track = currTrack.Value.Track;
                player.logger.Information("Starting track {Track}", track.Title);
                await using var inAudio = await httpClient.GetStreamAsync(track.Source.Url, cts.Token);
                await ffmpeg.ForSource(track.Source).From(inAudio).PipeAsync(cts.Token);
                currTrack = player.GetCurrentTrack(true);
                if (currTrack is null)
                    return;
            }
        }
        catch (Exception e)
        {
            player.logger.Error(e, "Error occured during playback");
        }
        finally
        {
            player.playbackTask = null;
            if (player.currentTrack is not null)
                player.currentTrack = player.currentTrack.GetValueOrDefault() with { EndTimeUtc = DateTime.UtcNow };

            player.logger.Information("Queue finished");
            if (player.audioClient.ConnectionState is not (ConnectionState.Disconnected or ConnectionState.Disconnecting
                ))
                await Task.WhenAny(pcm.FlushAsync(cts.Token), Task.Delay(TimeSpan.FromMinutes(1), cts.Token));
        }
    }

    private async ValueTask EnsureAudioClientAsync()
    {
        if (voiceChannel is null)
            throw new InvalidOperationException("Player is not attached");
        if (audioClient is null)
        {
            audioClient = await voiceChannel.ConnectAsync(true);
            audioClient.Disconnected += async _ =>
            {
                logger.Information("Audio client disconnected");
                await PauseAsync();
                KillAudioClient();
            };
        }
    }

    private void KillAudioClient()
    {
        audioClient?.Dispose();
        audioClient = null;
    }

    private CurrentTrack? GetCurrentTrack(bool ignoreCurrent = false)
    {
        if (!ignoreCurrent && currentTrack is not null)
            return currentTrack.GetValueOrDefault();

        if (!trackQueue.TryDequeue(out var track))
            return currentTrack = null;

        return currentTrack = new CurrentTrack
        {
            Track = track,
            StartTimeUtc = DateTime.UtcNow,
        };
    }

    private readonly record struct CurrentTrack(Track Track, DateTime StartTimeUtc, DateTime? EndTimeUtc = null)
    {
        public TimeSpan CurrentPosition
        {
            get
            {
                if (EndTimeUtc is null)
                    return TimeSpan.Zero;
                return EndTimeUtc.GetValueOrDefault() - StartTimeUtc;
            }
        }
    }
}