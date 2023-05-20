using Discord;
using Discord.Interactions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Treplo.Common;
using Treplo.SearchService;
using static Treplo.SearchService.SearchService;

namespace Treplo;

public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IClusterClient clusterClient;
    private readonly ILogger<PlayerModule> logger;
    private readonly SearchServiceClient searchServiceClient;

    public PlayerModule(
        IClusterClient clusterClient,
        SearchServiceClient searchServiceClient,
        ILogger<PlayerModule> logger
    )
    {
        this.clusterClient = clusterClient;
        this.searchServiceClient = searchServiceClient;
        this.logger = logger;
    }

    [SlashCommand(
        "play",
        "Enqueues first song found for provided query or starts playback if no query is provided",
        true,
        RunMode.Async
    )]
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
            var searchResult = await searchServiceClient.Search(
                new TrackSearchRequest
                {
                    Query = query,
                    Limit = 1,
                }
            ).ResponseStream.ReadAllAsync().FirstOrDefaultAsync();

            if (searchResult == default)
            {
                await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
                return;
            }

            track = searchResult.Track;
        }

        var session = clusterClient.GetGrain<ISessionGrain>(Context.Guild.Id.ToString());
        if (query is not null)
            await session.Enqueue(track!);
        await session.StartPlay(voiceChannel.Id);
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

        var tracks = await searchServiceClient.Search(
            new TrackSearchRequest
            {
                Query = query,
                Limit = limit,
            }
        ).ResponseStream.ReadAllAsync().ToArrayAsync();
        if (tracks.Length == 0)
        {
            await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
            return;
        }

        var session = clusterClient.GetGrain<ISessionGrain>(Context.Guild.Id.ToString());

        var tracksRequest = tracks.Select(x => x.Track);

        var searchId = await session.StartSearch(tracksRequest.ToArray());
        var componentBuilder = new ComponentBuilder()
            .WithSelectMenu(
                $"SearchSelectMenuId{searchId}",
                tracks
                    .Select(
                        (x, i) => new SelectMenuOptionBuilder(
                            x.Track.Title,
                            i.ToString(),
                            $"{x.Track.Author}. {x.SearchEngineName}"
                        )
                    )
                    .ToList()
            );
        await FollowupAsync("Select a song to enqueue", components: componentBuilder.Build());
    }

    [SlashCommand("pause", "Pauses currently playing song", true, RunMode.Async)]
    public async Task Pause()
    {
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await RespondAsync("User is not in a voice channel");
            return;
        }

        var session = clusterClient.GetGrain<ISessionGrain>(Context.Guild.Id.ToString());
        await session.Pause();
    }
}