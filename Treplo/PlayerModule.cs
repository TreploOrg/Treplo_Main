using Discord;
using Discord.Interactions;
using Serilog;
using Treplo.Clients;
using Treplo.Common.Models;

namespace Treplo;

public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger logger;
    private readonly ISearchServiceClient searchServiceClient;
    private readonly SessionManager sessionManager;

    public PlayerModule(
        ILogger logger,
        ISearchServiceClient searchServiceClient,
        SessionManager sessionManager
    )
    {
        this.searchServiceClient = searchServiceClient;
        this.sessionManager = sessionManager;
        this.logger = logger.ForContext<PlayerModule>();
    }

    [SlashCommand("play", "Enqueues first song found for provided query or starts playback if no query is provided",
        true, RunMode.Async)]
    public async Task Play(string? query = null)
    {
        await DeferAsync(true);
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await FollowupAsync("User is not in a voice channel");
            return;
        }

        Track? track = null;
        if (query is not null)
        {
            var searchResult = await searchServiceClient.SearchAsync(query, 1).FirstOrDefaultAsync();

            if (searchResult == default)
            {
                await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
                return;
            }

            track = searchResult.Track;
        }

        await sessionManager.EnqueueAsync(Context.Guild.Id, new TrackRequest(track.GetValueOrDefault(), StreamFormat));
        await sessionManager.StartPlayBackAsync(Context.Guild.Id, voiceChannel);
        await FollowupAsync("Starting playback");
    }

    [SlashCommand("search", "Searches for a song and allows to choose from found songs", true, RunMode.Async)]
    public async Task Search(string query, uint limit = 5)
    {
        await DeferAsync(true);
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await FollowupAsync("User is not in a voice channel");
            return;
        }

        var tracks = await searchServiceClient.SearchAsync(query, limit).ToArrayAsync();
        if (tracks.Length == 0)
        {
            await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
            return;
        }

        var tracksRequest = tracks.Select(x => new TrackRequest(x.Track, StreamFormat));

        var searchId = await sessionManager.StartSearchAsync(Context.Guild.Id, tracksRequest.ToArray());
        var componentBuilder = new ComponentBuilder()
            .WithSelectMenu(
                $"SearchSelectMenuId{searchId}",
                tracks
                    .Select((x, i) => new SelectMenuOptionBuilder(x.Track.Title, i.ToString(),
                        $"{x.Track.Author}. {x.SearchEngineName}"))
                    .ToList()
            );
        await FollowupAsync("Select a song to enqueue", components: componentBuilder.Build());
    }

    private StreamFormatRequest StreamFormat { get; } = new(2, new Container("s16le"), null, Frequency: 48000);
}