using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace Treplo;

public sealed class DiscordBotRunner : IHostedService, IDisposable, IAsyncDisposable
{
    private readonly DiscordSocketClient client;
    private readonly IDateTimeManager dateTimeManager;
    private readonly InteractionService interactionService;
    private readonly ILogger logger;
    private readonly IPlayerSessionsManager playerSessionsManager;
    private readonly IServiceProvider serviceProvider;
    private readonly IOptions<DiscordClientSettings> settings;

    public DiscordBotRunner(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IDateTimeManager dateTimeManager,
        IPlayerSessionsManager playerSessionsManager,
        IOptions<DiscordClientSettings> settings,
        ILogger logger
    )
    {
        this.client = client;
        this.interactionService = interactionService;
        this.serviceProvider = serviceProvider;
        this.dateTimeManager = dateTimeManager;
        this.playerSessionsManager = playerSessionsManager;
        this.settings = settings;
        this.logger = logger.ForContext<DiscordBotRunner>();

        client.Ready += ClientOnReady;
        client.Log += ClientLog;
        client.InteractionCreated += ClientOnInteractionCreated;
        client.SelectMenuExecuted += ClientOnSelectMenuExecuted;

        this.interactionService.SlashCommandExecuted += InteractionServiceOnSlashCommandExecuted;
        this.interactionService.Log += InteractionServiceLog;
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
    }

    public void Dispose()
    {
        client.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await client.LoginAsync(TokenType.Bot, settings.Value.Token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        logger.Information("Performing graceful shutdown");
        await client.LogoutAsync();
        await DisposeAsync();
    }

    private static LogEventLevel GetSerilogLevel(LogSeverity logMessageSeverity)
    {
        return logMessageSeverity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => throw new ArgumentOutOfRangeException(nameof(logMessageSeverity), logMessageSeverity, null),
        };
    }

    private async Task ClientOnSelectMenuExecuted(SocketMessageComponent component)
    {
        await component.DeferAsync(true);
        var data = component.Data;
        var id = data.CustomId;
        var user = component.User as IGuildUser;
        if (id.StartsWith(IPlayerSessionsManager.SearchSelectMenuId)
            && Guid.TryParse(id.AsSpan(IPlayerSessionsManager.SearchSelectMenuId.Length), out var searchId)
            && component.GuildId is not null
            && int.TryParse(data.Values.FirstOrDefault(), out var index)
        )
            await Task.Factory.StartNew(async () =>
            {
                var track = await playerSessionsManager.RespondToSearchAsync(
                    component.GuildId.GetValueOrDefault(),
                    user.VoiceChannel,
                    searchId,
                    index);
                var trackTitle = track.Title;
                await component.UpdateAsync(x => { x.Content = $"Selected track {trackTitle}"; });
            });
        else
            await component.UpdateAsync(x => { x.Content = "Something went wrong"; });
    }

    private async Task ClientOnReady()
    {
        await Task.WhenAll(client.Guilds.AsParallel()
            .Select(async x => await (Task)interactionService.RegisterCommandsToGuildAsync(x.Id)));
    }

    private async Task InteractionServiceOnSlashCommandExecuted(
        SlashCommandInfo? command,
        IInteractionContext context,
        IResult result
    )
    {
        if (result.IsSuccess)
        {
            logger.Information("Successfully handled slash command {Command}", command!.Name);
            return;
        }

        var embedBuilder = new EmbedBuilder()
            .WithColor(Color.DarkRed)
            .WithTimestamp(dateTimeManager.UtcNow)
            .WithTitle("Error occured");

        if (command is not null)
        {
            logger.Error("Error occured during command \"{Command}\" handling: {ErrorType} - {ErrorMessage}",
                command.Name, result.Error, result.ErrorReason);
            embedBuilder.WithDescription(
                $"Error occured during command \"{command.Name}\" handling: {result.Error!} - {result.ErrorReason}");
            await context.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embedBuilder.Build());
        }
        else
        {
            logger.Error("Error occured during command handling: {ErrorType} - {ErrorMessage}",
                result.Error, result.ErrorReason);
            embedBuilder.WithDescription(
                $"Error occured during command handling: {result.Error!} - {result.ErrorReason}");
            await context.Interaction.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
        }
    }

    private async Task ClientOnInteractionCreated(SocketInteraction interaction)
    {
        logger.Information("Received interaction: {InteractionType}", interaction.Type);
        var interactionContext = new SocketInteractionContext(client, interaction);
        await interactionService.ExecuteCommandAsync(interactionContext, serviceProvider);
    }

    private Task ClientLog(LogMessage logMessage) => SourcedLog(logMessage, "Client");

    private Task InteractionServiceLog(LogMessage logMessage) => SourcedLog(logMessage, "Interaction Service");

    private Task SourcedLog(LogMessage logMessage, string source)
    {
        logger.Write(GetSerilogLevel(logMessage.Severity),
            logMessage.Exception,
            "[{ExternalSource} - {InternalSource}] {Message}",
            source,
            logMessage.Source,
            logMessage.Message ?? string.Empty);
        return Task.CompletedTask;
    }
}