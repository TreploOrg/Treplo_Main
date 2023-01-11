using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Treplo.Clients;
using Treplo.Common.Models;

namespace Treplo;

public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<PlayerModule> logger;
    private readonly SearchServiceClient searchServiceClient;
    private readonly SessionManager sessionManager;

    public PlayerModule(
        SearchServiceClient searchServiceClient,
        SessionManager sessionManager,
        ILogger<PlayerModule> logger
    )
    {
        this.searchServiceClient = searchServiceClient;
        this.sessionManager = sessionManager;
        this.logger = logger;
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
        
        var session = sessionManager.GetSession(Context.Guild.Id);
        await session.Enqueue(track.GetValueOrDefault());
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

        var tracks = await searchServiceClient.SearchAsync(query, limit).ToArrayAsync();
        if (tracks.Length == 0)
        {
            await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
            return;
        }
        
        var session = sessionManager.GetSession(Context.Guild.Id);

        var tracksRequest = tracks.Select(x => x.Track);

        var searchId = await session.StartSearch(tracksRequest.ToArray());
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
}