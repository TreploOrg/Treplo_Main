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
    private readonly ILogger logger;
    private readonly InteractionService interactionService;
    private readonly IServiceProvider serviceProvider;
    private readonly IDateTimeManager dateTimeManager;
    private readonly IOptions<DiscordClientSettings> settings;

    public DiscordBotRunner(DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IDateTimeManager dateTimeManager,
        IOptions<DiscordClientSettings> settings,
        ILogger logger)
    {
        this.client = client;
        this.interactionService = interactionService;
        this.serviceProvider = serviceProvider;
        this.dateTimeManager = dateTimeManager;
        this.settings = settings;
        this.logger = logger.ForContext<DiscordBotRunner>();

        client.Ready += ClientOnReady;
        client.Log += ClientLog;
        client.InteractionCreated += ClientOnInteractionCreated;

        this.interactionService.SlashCommandExecuted += InteractionServiceOnSlashCommandExecuted;
        this.interactionService.Log += InteractionServiceLog;
    }

    private async Task ClientOnReady()
    {
        await Task.WhenAll(client.Guilds.AsParallel()
            .Select(async x => await (Task)interactionService.RegisterCommandsToGuildAsync(x.Id)));
    }

    private async Task InteractionServiceOnSlashCommandExecuted(SlashCommandInfo? command, IInteractionContext context,
        IResult result)
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

    private Task ClientLog(LogMessage logMessage)
    {
        return SourcedLog(logMessage, "Client");
    }

    private Task InteractionServiceLog(LogMessage logMessage)
    {
        return SourcedLog(logMessage, "Interaction Service");
    }

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
            _ => throw new ArgumentOutOfRangeException(nameof(logMessageSeverity), logMessageSeverity, null)
        };
    }

    public void Dispose()
    {
        client.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
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
}