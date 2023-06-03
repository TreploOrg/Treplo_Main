using Microsoft.Extensions.Logging;
using Treplo.Common;
using Treplo.Converters;
using Treplo.Helpers;
using Treplo.Player;
using Treplo.PlayersService;
using static Treplo.PlayersService.PlayersService;

namespace Treplo;

public sealed class SessionGrain : Grain, ISessionGrain
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
    private readonly ILogger<SessionGrain> logger;
    private readonly IPlayer player;
    private readonly PlayersServiceClient playerServiceClient;

    public SessionGrain(
        PlayersServiceClient playerServiceClient,
        IPlayerFactory playerFactory,
        ILogger<SessionGrain> logger
    )
    {
        this.playerServiceClient = playerServiceClient;
        player = playerFactory.Create(this.GetGuildId(), StreamFormat);
        this.logger = logger;
    }

    public async ValueTask StartPlay(ulong voiceChannelId)
    {
        logger.LogInformation(
            "Trying to start playback in voice channel {VoiceChannelId} is session {SessionId}",
            voiceChannelId,
            this.GetGuildId()
        );

        await player.AttachTo(voiceChannelId);
        await player.Play();
    }

    public ValueTask<Guid> StartSearch(Track[] searchTracks)
    {
        var searchId = Guid.NewGuid();
        logger.LogInformation(
            "Starting search of {NumSearchItems} items with id {SearchId} in session {SessionId}",
            searchTracks.Length,
            searchId,
            this.GetGuildId()
        );
        activeSearches[searchId] = searchTracks;
        return ValueTask.FromResult(searchId);
    }

    public async ValueTask<Track> EndSearch(Guid searchSessionId, uint trackId)
    {
        if (!activeSearches.Remove(searchSessionId, out var requests))
            throw new ArgumentException("Unknown session id", nameof(searchSessionId));
        logger.LogInformation(
            "Responding to search {SearchId} is session {SessionId}",
            searchSessionId,
            this.GetGuildId()
        );
        var track = requests[trackId];
        await Enqueue(track);
        return track;
    }

    public async ValueTask Pause()
    {
        logger.LogInformation("Pausing playback in session {SessionId}", this.GetGuildId());
        await player.Pause();
    }

    public async ValueTask Skip()
    {
        logger.LogInformation("Skipping playback in session {SessionId}", this.GetGuildId());
        await player.Skip();
    }

    public async ValueTask Enqueue(Track track)
    {
        logger.LogInformation(
            "Enqueueing track {TrackTitle} in session {SessionId}",
            track.Title,
            this.GetGuildId()
        );
        await playerServiceClient.EnqueueAsync(
            new EnqueueRequest
            {
                PlayerRequest = new PlayerIdentifier
                {
                    PlayerId = this.GetGuildId().ToString(),
                },
                Track = track,
            }
        );
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Session {SessionId} alive", this.GetGuildId());
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        logger.LogInformation("Session {SessionId} un-alive", this.GetGuildId());
        await player.Disconnect();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}