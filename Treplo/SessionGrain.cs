using Microsoft.Extensions.Logging;
using Treplo.Common;
using Treplo.Converters;
using Treplo.Helpers;
using Treplo.Playback;
using Treplo.PlayersService;
using static Treplo.PlayersService.PlayersService;
using IAudioClient = Treplo.Playback.IAudioClient;

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

    private readonly PlayersServiceClient playerServiceClient;
    private readonly IRawAudioSource rawAudioSource;
    private readonly IAudioConverterFactory converterFactory;
    private readonly IAudioClient audioClient;
    private readonly ILogger<SessionGrain> logger;
    private readonly Dictionary<Guid, Track[]> activeSearches = new();

    private CancellationTokenSource playbackCancellation = new();
    private Task? playbackTask;

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


    public async ValueTask StartPlay(ulong voiceChannelId)
    {
        if (audioClient.ChannelId is { } currentChannelId) // if we are connected to some channel
        {
            if (currentChannelId != voiceChannelId) // if it's different channel
            {
                if (playbackTask is not null) // if there is something playing
                {
                    await WaitPlaybackStop();
                    RotateCts();
                }

                await audioClient.ConnectToChannel(voiceChannelId); // connect to required channel
            }

            playbackTask ??=
                StartPlaybackCore(
                    playbackCancellation.Token
                ); // start playback on connected channel if it's not already started

            return;
        }

        await audioClient.ConnectToChannel(voiceChannelId);
        playbackTask = StartPlaybackCore(playbackCancellation.Token);
    }


    private async Task StartPlaybackCore(CancellationToken cancellationToken)
    {
        var dequeueRequest = new DequeueRequest
        {
            PlayerRequest = new PlayerIdentifier
            {
                PlayerId = this.GetPrimaryKeyString(),
            },
        };
        while (audioClient.ChannelId is not null)
        {
            try
            {
                var result = await playerServiceClient.DequeueAsync(
                    dequeueRequest,
                    cancellationToken: cancellationToken
                );
                if (result.Track is not { } track)
                    break;

                var audioSource = rawAudioSource.GetAudioPipe(track.Source);
                var audioConverter = converterFactory.Create(track.Source, in StreamFormat);

                var inPipeTask = audioSource.PipeThrough(audioConverter.Input, cancellationToken);
                var outPipeTask = audioClient.ConsumeAudioPipe(audioConverter.Output, cancellationToken);
                var conversionTask = audioConverter.Start(cancellationToken);

                await Task.WhenAll(inPipeTask, outPipeTask, conversionTask);
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

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await DisposeAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await WaitPlaybackStop();
        await audioClient.DisposeAsync();
        playbackCancellation.Dispose();
    }

    private async ValueTask WaitPlaybackStop()
    {
        if (playbackTask is null)
            return;
        if (!playbackTask.IsCompleted)
        {
            playbackCancellation.Cancel(); // cancel playback
            await playbackTask; // wait for the task to finish
        }

        playbackTask = null; // null out the task 
    }

    private void RotateCts()
    {
        playbackCancellation.Dispose();
        playbackCancellation = new CancellationTokenSource();
    }
}