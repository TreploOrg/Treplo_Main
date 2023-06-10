using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stateless;
using Treplo.Common;
using Treplo.Converters;
using Treplo.Helpers;
using Treplo.Playback;
using Treplo.PlayersService;
using static Treplo.Player.PlayerStatus;

namespace Treplo.Player;

public interface IPlayer
{
    PlayerStatus Status { get; }
    Track? CurrentTrack { get; }
    TimeSpan? PlaybackTime { get; }
    ValueTask Play();
    ValueTask Pause();
    ValueTask Skip();
    ValueTask AttachTo(ulong channelId);
    ValueTask Disconnect();
}

public sealed class Player : IPlayer, IAsyncDisposable
{
    private readonly IAudioClient audioClient;
    private readonly IRawAudioSource audioSource;
    private readonly Lazy<DequeueRequest> cachedDequeue;
    private readonly StateMachine<PlayerStatus, PlayerTriggers>.TriggerWithParameters<ulong> connectTrigger;
    private readonly IAudioConverterFactory converterFactory;
    private readonly ulong guildId;
    private readonly StreamFormatRequest formatRequest;
    private readonly PlayersService.PlayersService.PlayersServiceClient playersServiceClient;
    private readonly ILogger<Player> logger;
    private readonly StateMachine<PlayerStatus, PlayerTriggers> stateMachine;
    private CancellationTokenSource pauseCts = new();

    private Track? currentTrack;
    private Func<Task>? endCallback;
    private long? endTimestamp;
    private Task? playback;
    private long? startTimestamp;

    public Player(
        PlayersService.PlayersService.PlayersServiceClient playersServiceClient,
        ILogger<Player> logger,
        IRawAudioSource audioSource,
        IAudioClient audioClient,
        IAudioConverterFactory converterFactory,
        ulong guildId,
        StreamFormatRequest formatRequest
    )
    {
        this.playersServiceClient = playersServiceClient;
        this.logger = logger;
        this.audioSource = audioSource;
        this.audioClient = audioClient;
        this.converterFactory = converterFactory;
        this.guildId = guildId;
        this.formatRequest = formatRequest;
        cachedDequeue = new Lazy<DequeueRequest>(
            () => new DequeueRequest
            {
                PlayerRequest = new PlayerIdentifier
                {
                    PlayerId = guildId.ToString(),
                },
            }
        );
        stateMachine = new StateMachine<PlayerStatus, PlayerTriggers>(None, FiringMode.Queued);
        connectTrigger = stateMachine.SetTriggerParameters<ulong>(PlayerTriggers.Connect);
        ConfigureStateMachine();
    }

    public async ValueTask DisposeAsync() => await stateMachine.FireAsync(PlayerTriggers.Disconnect);

    public PlayerStatus Status => stateMachine.State;

    public Track? CurrentTrack
    {
        get => stateMachine.State is None or NoTrack ? null : currentTrack;
        private set => currentTrack = value;
    }

    public TimeSpan? PlaybackTime
    {
        get
        {
            if (startTimestamp is not { } startStamp)
                return null;

            if (endTimestamp is { } endStamp)
                return Stopwatch.GetElapsedTime(startStamp, endStamp);

            return Stopwatch.GetElapsedTime(startStamp);
        }
    }

    public async ValueTask Play() => await stateMachine.FireAsync(PlayerTriggers.Play);

    public async ValueTask Pause() => await stateMachine.FireAsync(PlayerTriggers.Pause);

    public async ValueTask Skip() => await stateMachine.FireAsync(PlayerTriggers.Skip);

    public async ValueTask AttachTo(ulong channelId) => await stateMachine.FireAsync(connectTrigger, channelId);

    public async ValueTask Disconnect() => await stateMachine.FireAsync(PlayerTriggers.Disconnect);

    private void ConfigureStateMachine()
    {
        stateMachine.OnUnhandledTriggerAsync((state, trigger) =>
            {
                logger.LogWarning("Not registered transition occured in {PlayerId}: from {Status} via {Trigger}", guildId, state, trigger);
                return Task.CompletedTask;
            }
        );
        stateMachine.Configure(None)
            .Permit(PlayerTriggers.Connect, Connected);
        stateMachine.Configure(NoTrack)
            .SubstateOf(None)
            .Ignore(PlayerTriggers.Skip)
            .Permit(PlayerTriggers.Disconnect, NotConnected)
            .OnEntry(UnsetTrack);
        stateMachine.Configure(NotConnected)
            .SubstateOf(None)
            .Ignore(PlayerTriggers.Disconnect);

        stateMachine.Configure(Connected)
            .OnEntryFromAsync(connectTrigger, OnConnect)
            .Permit(PlayerTriggers.Disconnect, NotConnected)
            .Permit(PlayerTriggers.Play, Playing)
            .PermitReentry(PlayerTriggers.Connect);

        stateMachine.Configure(CanStart)
            .Permit(PlayerTriggers.Play, Playing)
            .Permit(PlayerTriggers.Disconnect, NotConnected)
            .Permit(PlayerTriggers.Skip, NoTrack);

        stateMachine.Configure(Playing)
            .Permit(PlayerTriggers.Pause, Paused)
            .InternalTransition(
                PlayerTriggers.Skip,
                _ =>
                {
                    StopPlay();
                    CurrentTrack = null;
                    startTimestamp = null;
                    StartPlay();
                }
            )
            .Permit(PlayerTriggers.QueueFinished, NoTrack)
            .Permit(PlayerTriggers.Disconnect, NotConnected)
            .Ignore(PlayerTriggers.Play)
            .InternalTransitionAsync(
                connectTrigger,
                async (channelId, _) =>
                {
                    if (audioClient.ChannelId == channelId)
                        return;
                    StopPlay();
                    await OnConnect(channelId);
                    StartPlay();
                }
            )
            .OnEntry(StartPlay)
            .OnExit(StopPlay);

        stateMachine.Configure(Paused)
            .Permit(PlayerTriggers.Play, Playing)
            .Permit(PlayerTriggers.Skip, NoTrack)
            .Permit(PlayerTriggers.Disconnect, NotConnected)
            .Ignore(PlayerTriggers.Pause)
            .OnEntry(OnPause)
            .InternalTransitionAsync(connectTrigger, (channelId, _) => OnConnect(channelId));

        stateMachine.OnTransitioned(
            transition => logger.LogInformation(
                "Transition in {PlayerId}: from {Status} to {Destination} via {Trigger}",
                guildId,
                transition.Source,
                transition.Destination,
                transition.Trigger
            )
        );
    }

    private void OnPause()
    {
        endTimestamp = Stopwatch.GetTimestamp();
    }

    private void UnsetTrack()
    {
        CurrentTrack = null;
        startTimestamp = null;
        endTimestamp = null;
    }

    private void StopPlay()
    {
        pauseCts.Cancel();
        pauseCts.Dispose();
        pauseCts = new CancellationTokenSource();
        playback = null;
    }

    private void StartPlay()
    {
        var token = pauseCts.Token;
        playback = StartPlayCore(token);

        async Task StartPlayCore(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (CurrentTrack is null && !await GetNextTrack(cancellationToken))
                        return;
                    var source = CurrentTrack!.Source;
                    var audioInputPipe = audioSource.GetAudioPipe(source);
                    var converter = converterFactory.Create(source, in formatRequest, PlaybackTime);


                    startTimestamp = Stopwatch.GetTimestamp();
                    endTimestamp = null;
                    var audioInTask = audioInputPipe.PipeThrough(converter.Input, cancellationToken);
                    var conversionTask = converter.Start(cancellationToken);
                    var audioOutTask = audioClient.ConsumeAudioPipe(converter.Output, cancellationToken);

                    await Task.WhenAll(audioInTask, audioOutTask, conversionTask);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        CurrentTrack = null;
                        startTimestamp = null;
                    }
                }
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    await stateMachine.FireAsync(PlayerTriggers.QueueFinished);
            }
        }
    }

    private async Task<bool> GetNextTrack(CancellationToken cancellationToken)
    {
        var response = await playersServiceClient.DequeueAsync(
            cachedDequeue.Value,
            cancellationToken: cancellationToken
        );
        CurrentTrack = response.Track;
        return response.Track is not null;
    }

    private Task OnConnect(ulong channelId) => audioClient.ConnectToChannel(channelId);
}