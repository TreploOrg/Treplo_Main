using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Treplo.Helpers;

namespace Treplo;

public sealed class DiscordBotRunner : IHostedService
{
    private readonly DiscordSocketClient client;
    private readonly IClusterClient clusterClient;
    private readonly ITrackEmbedBuilder embedBuilder;
    private readonly IDateTimeManager dateTimeManager;
    private readonly InteractionService interactionService;
    private readonly ILogger<DiscordBotRunner> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IOptions<DiscordClientSettings> settings;

    public DiscordBotRunner(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IDateTimeManager dateTimeManager,
        IClusterClient clusterClient,
        ITrackEmbedBuilder embedBuilder,
        IOptions<DiscordClientSettings> settings,
        ILogger<DiscordBotRunner> logger
    )
    {
        this.client = client;
        this.interactionService = interactionService;
        this.serviceProvider = serviceProvider;
        this.dateTimeManager = dateTimeManager;
        this.clusterClient = clusterClient;
        this.embedBuilder = embedBuilder;
        this.settings = settings;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await interactionService.AddModuleAsync<PlayerModule>(serviceProvider);

        client.Ready += ClientOnReady;
        client.Log += ClientLog;
        client.InteractionCreated += ClientOnInteractionCreated;
        client.SelectMenuExecuted += ClientOnSelectMenuExecuted;

        interactionService.SlashCommandExecuted += InteractionServiceOnSlashCommandExecuted;
        interactionService.Log += InteractionServiceLog;

        await client.LoginAsync(TokenType.Bot, settings.Value.Token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        logger.LogInformation("Performing graceful shutdown");
        await client.LogoutAsync();
    }

    private static LogLevel GetSerilogLevel(LogSeverity logMessageSeverity)
    {
        return logMessageSeverity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => throw new ArgumentOutOfRangeException(nameof(logMessageSeverity), logMessageSeverity, null),
        };
    }

    private async Task ClientOnSelectMenuExecuted(SocketMessageComponent component)
    {
        await component.DeferAsync(true);
        var data = component.Data;
        var id = data.CustomId;
        if (id.StartsWith("SearchSelectMenuId")
            && Guid.TryParse(id.AsSpan("SearchSelectMenuId".Length), out var searchId)
            && component.GuildId is not null
            && uint.TryParse(data.Values.FirstOrDefault(), out var index)
        )
            await Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        var session =
                            clusterClient.GetGrain(component.GuildId.GetValueOrDefault());
                        var track = await session.EndSearch(searchId, index);

                        var embed = embedBuilder.ForTrack(track).WithAuthor(client.CurrentUser).Build();
                        await component.FollowupAsync("Adding track to queue", embed: embed, ephemeral: true);

                        var guildUser = component.User as IGuildUser;
                        await session.StartPlay(guildUser.VoiceChannel.Id);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error during search responding");
                    }
                }
            );
        else
            await component.UpdateAsync(x => { x.Content = "Something went wrong"; });
    }

    private async Task ClientOnReady()
    {
        foreach (var guild in client.Guilds)
        {
            await interactionService.RegisterCommandsToGuildAsync(guild.Id);
        }
    }

    private async Task InteractionServiceOnSlashCommandExecuted(
        SlashCommandInfo? command,
        IInteractionContext context,
        IResult result
    )
    {
        if (result.IsSuccess)
        {
            logger.LogInformation("Successfully handled slash command {Command}", command!.Name);
            return;
        }

        var embedBuilder = new EmbedBuilder()
            .WithColor(Color.DarkRed)
            .WithTimestamp(dateTimeManager.UtcNow)
            .WithTitle("Error occured");

        if (command is not null)
        {
            logger.LogError(
                "Error occured during command \"{Command}\" handling: {ErrorType} - {ErrorMessage}",
                command.Name,
                result.Error,
                result.ErrorReason
            );
            embedBuilder.WithDescription(
                $"Error occured during command \"{command.Name}\" handling: {result.Error!} - {result.ErrorReason}"
            );
            await context.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embedBuilder.Build());
        }
        else
        {
            logger.LogError(
                "Error occured during command handling: {ErrorType} - {ErrorMessage}",
                result.Error,
                result.ErrorReason
            );
            embedBuilder.WithDescription(
                $"Error occured during command handling: {result.Error!} - {result.ErrorReason}"
            );
            await context.Interaction.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
        }
    }

    private async Task ClientOnInteractionCreated(SocketInteraction interaction)
    {
        logger.LogInformation("Received interaction: {InteractionType}", interaction.Type);
        var interactionContext = new SocketInteractionContext(client, interaction);
        await interactionService.ExecuteCommandAsync(interactionContext, serviceProvider);
    }

    private Task ClientLog(LogMessage logMessage) => SourcedLog(logMessage, "Client");

    private Task InteractionServiceLog(LogMessage logMessage) => SourcedLog(logMessage, "Interaction Service");

    private Task SourcedLog(LogMessage logMessage, string source)
    {
        logger.Log(
            GetSerilogLevel(logMessage.Severity),
            logMessage.Exception,
            "[{ExternalSource} - {InternalSource}] {Message}",
            source,
            logMessage.Source,
            logMessage.Message ?? string.Empty
        );
        return Task.CompletedTask;
    }
}