using Discord;
using Discord.Interactions;
using Serilog;
using Treplo.Models;

namespace Treplo;

public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISearchEngineManager searchEngineManager;
    private readonly IPlayerSessionsManager playerSessionsManager;

    private readonly ILogger logger;

    public PlayerModule(ILogger logger,
        ISearchEngineManager searchEngineManager,
        IPlayerSessionsManager playerSessionsManager)
    {
        this.searchEngineManager = searchEngineManager;
        this.playerSessionsManager = playerSessionsManager;
        this.logger = logger.ForContext<PlayerModule>();
    }

    [SlashCommand("play", "Enqueues first song found for provided query or starts playback if no query is provided",
        true, RunMode.Async)]
    public async Task Play(string? query = null)
    {
        await DeferAsync(ephemeral: true);
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await FollowupAsync("User is not in a voice channel");
            return;
        }

        Track? track = null;
        if (query is not null)
        {
            var searchResult = await searchEngineManager.SearchAsync(query).FirstOrDefaultAsync();

            if (searchResult == default)
            {
                await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
                return;
            }

            track = searchResult.Track;
        }

        await playerSessionsManager.PlayAsync(Context.Guild.Id, voiceChannel, track);
        await FollowupAsync("Starting playback");
    }

    [SlashCommand("search", "Searches for a song and allows to choose from found songs", true, RunMode.Async)]
    public async Task Search(string query, int limit = 5)
    {
        await DeferAsync(ephemeral: true);
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await FollowupAsync("User is not in a voice channel");
            return;
        }

        var tracks = await searchEngineManager.SearchAsync(query).Take(limit).ToArrayAsync();
        if (tracks.Length == 0)
        {
            await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
            return;
        }

        var searchId = await playerSessionsManager.StartSearchAsync(Context.Guild.Id, voiceChannel, tracks);
        var componentBuilder = new ComponentBuilder()
            .WithSelectMenu(
                $"{IPlayerSessionsManager.SearchSelectMenuId}{searchId}",
                tracks
                    .Select((x, i) => new SelectMenuOptionBuilder(x.Track.Title, i.ToString(),
                        description: $"{x.Track.Author}. {x.SearchEngineName}"))
                    .ToList()
            );
        await FollowupAsync("Select a song to enqueue", components: componentBuilder.Build());
    }
}