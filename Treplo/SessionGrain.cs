using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Treplo.Common;
using Treplo.Converters;
using Treplo.Helpers;
using Treplo.Playback;
using Treplo.PlayersService;
using static Treplo.PlayersService.PlayersService;

namespace Treplo;

public sealed class SessionGrain : Grain, ISessionGrain, IAsyncDisposable
{
    private static readonly StreamFormatRequest StreamFormat = new()
    {
        Channels = 2,
        Frequency = 48000,
        Container = new Container
        {
            Name = "s16le",
        },
    };

    private readonly Dictionary<Guid, Track[]> activeSearches = new();
    private readonly IAudioClient audioClient;
    private readonly IAudioConverterFactory converterFactory;
    private readonly ILogger<SessionGrain> logger;

    private readonly PlayersServiceClient playerServiceClient;
    private readonly IRawAudioSource rawAudioSource;

    private CancellationTokenSource playbackCancellation = new();
    private Task<LastTrackState?>? playbackTask;
    private LastTrackState? lastPlaybackNotFinishedState;

    public SessionGrain(
        PlayersServiceClient playerServiceClient,
        IRawAudioSource rawAudioSource,
        IAudioConverterFactory converterFactory,
        IAudioClient audioClient,
        ILogger<SessionGrain> logger
    )
    {
        this.playerServiceClient = playerServiceClient;
        this.rawAudioSource = rawAudioSource;
        this.converterFactory = converterFactory;
        this.audioClient = audioClient;
        this.logger = logger;
    }

    public async ValueTask DisposeAsync()
    {
        await WaitPlaybackStop();
        await audioClient.DisposeAsync();
        playbackCancellation.Dispose();
    }


    public async ValueTask StartPlay(ulong voiceChannelId)
    {
        if (audioClient.ChannelId is { } currentChannelId) // if we are connected to some channel
        {
            if (currentChannelId != voiceChannelId) // if it's different channel
            {
                if (playbackTask is not null) // if there is something playing
                {
                    await Pause();
                }

                await ConnectToChannel(voiceChannelId); // connect to required channel
            }

            playbackTask ??=
                StartPlaybackCore(
                    playbackCancellation.Token
                ); // start playback on connected channel if it's not already started

            return;
        }

        await ConnectToChannel(voiceChannelId);
        playbackTask = StartPlaybackCore(playbackCancellation.Token);

        async Task ConnectToChannel(ulong channelId)
        {
            logger.LogInformation(
                "Connecting session {SessionId} to voice channel {VoiceChannelId}",
                this.GetPrimaryKeyString(),
                channelId
            );
            await audioClient.ConnectToChannel(channelId);
        }
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

    public async ValueTask Pause()
    {
        lastPlaybackNotFinishedState = await WaitPlaybackStop();
        RotateCts();
    }

    public async ValueTask Enqueue(Track track)
    {
        await playerServiceClient.EnqueueAsync(
            new EnqueueRequest
            {
                PlayerRequest = new PlayerIdentifier
                {
                    PlayerId = this.GetPrimaryKeyString(),
                },
                Track = track,
            }
        );
    }


    private async Task<LastTrackState?> StartPlaybackCore(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting playback in session {SessionId}", this.GetPrimaryKeyString());
        var dequeueRequest = new DequeueRequest
        {
            PlayerRequest = new PlayerIdentifier
            {
                PlayerId = this.GetPrimaryKeyString(),
            },
        };
        Track? lastTrack = null;
        TimeSpan? lastTrackPlaybackTime = null;
        while (audioClient.ChannelId is not null)
        {
            try
            {
                logger.LogInformation("Acquiring next track in session {SessionId}", this.GetPrimaryKeyString());
                var result = await GetNextTrack(dequeueRequest, cancellationToken);
                if (result is not { } track)
                {
                    logger.LogInformation("No next track in session {SessionId}", this.GetPrimaryKeyString());
                    break;
                }

                logger.LogInformation("Acquired track {TrackTitle} in session {SessionId}", track.Track.Title, this.GetPrimaryKeyString());
                lastTrack = track.Track;
                lastTrackPlaybackTime = await PlayTrack(track, cancellationToken);
                logger.LogInformation("Track {TrackTitle} finished in session {SessionId}", track.Track.Title, this.GetPrimaryKeyString());
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception during playback in grain {SessionId}", this.GetPrimaryKeyString());
            }
        }

        playbackTask = null;
        return lastTrackPlaybackTime is { } time && lastTrack is not null ? new LastTrackState(lastTrack, time) : null;
    }

    private async ValueTask<TrackToPlay?> GetNextTrack(
        DequeueRequest dequeueRequest,
        CancellationToken cancellationToken
    )
    {
        if (lastPlaybackNotFinishedState is { Track: var track, StopTime: var stopTime })
            return new TrackToPlay(
                track,
                GetStartTime(stopTime)
            );

        var result = await playerServiceClient.DequeueAsync(
            dequeueRequest,
            cancellationToken: cancellationToken
        );

        if (result.Track is { } trackResult)
            return new TrackToPlay(trackResult, null);

        return null;

        static TimeSpan? GetStartTime(TimeSpan timeSpan) => timeSpan.TotalSeconds <= 1
            ? null
            : timeSpan - TimeSpan.FromSeconds(1);
    }

    private async Task<TimeSpan> PlayTrack(TrackToPlay track, CancellationToken cancellationToken)
    {
        Console.WriteLine("Acquiring audio pipe");
        var audioSource = rawAudioSource.GetAudioPipe(track.Track.Source);
        Console.WriteLine("Acquiring converter");
        var audioConverter = converterFactory.Create(track.Track.Source, in StreamFormat, track.StartTime);

        Console.WriteLine("Starting pipe in converter");
        var inPipeTask = audioSource.PipeThrough(audioConverter.Input, cancellationToken);
        Console.WriteLine("Starting pipe out of converter");
        var outPipeTask = audioClient.ConsumeAudioPipe(audioConverter.Output, cancellationToken);
        Console.WriteLine("Starting converter");
        var conversionTask = audioConverter.Start(cancellationToken);
        var startTime = Stopwatch.GetTimestamp();
        Console.WriteLine("Waiting for tasks");
        await Task.WhenAll(inPipeTask, outPipeTask, conversionTask);
        return Stopwatch.GetElapsedTime(startTime);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await DisposeAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    private async ValueTask<LastTrackState?> WaitPlaybackStop()
    {
        var local = Interlocked.Exchange(ref playbackTask, null);
        if (local is null)
            return null;

        if (!local.IsCompleted)
        {
            playbackCancellation.Cancel(); // cancel playback
            await local; // wait for the task to finish
        }
        
        playbackTask = null; // null out the task 
        return local.Result;
    }

    private void RotateCts()
    {
        playbackCancellation.Dispose();
        playbackCancellation = new CancellationTokenSource();
    }

    private readonly record struct TrackToPlay(Track Track, TimeSpan? StartTime);

    private readonly record struct LastTrackState(Track Track, TimeSpan StopTime);
}