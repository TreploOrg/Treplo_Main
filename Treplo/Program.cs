using System.Reflection;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Treplo.Clients;

namespace Treplo;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostCtx, services) =>
            {
                services.AddHttpClient();
                SetupDiscordBot(services, hostCtx);
                services.AddSingleton<IDateTimeManager, DateTimeManager>();
                services
                    .AddSingleton<IPlayerSessionsManager, PlayerSessionsManager>();

                services.Configure<SearchServiceClientSettings>(
                    hostCtx.Configuration
                        .GetSection(nameof(SearchServiceClientSettings)));
                services.AddOptions<SearchServiceClientSettings>();

                services.AddTransient<ISearchServiceClient, SearchServiceClient>();
            })
            .UseSerilog((hostingContext, _, loggerConfiguration) =>
            {
                Console.OutputEncoding = Encoding.UTF8;
                loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                    .MinimumLevel.Override("System", LogEventLevel.Error)
                    .Enrich.FromLogContext()
                    .WriteTo.Console();
            });

        var host = hostBuilder.Build();

        await host.RunAsync();
    }

    private static void SetupDiscordBot(IServiceCollection services, HostBuilderContext hostCtx)
    {
        services.Configure<DiscordClientSettings>(
            hostCtx.Configuration.GetSection(nameof(DiscordClientSettings)));
        services.AddOptions<DiscordClientSettings>();
        services.AddSingleton(ctx =>
        {
            var settings = ctx.GetRequiredService<IOptions<DiscordClientSettings>>();
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = settings.Value.Intents
                    .Aggregate(GatewayIntents.None,
                        (seed, cur) => seed | cur),
            });

            return client;
        });
        services.AddSingleton(ctx =>
            {
                var interactionService =
                    new InteractionService(ctx.GetRequiredService<DiscordSocketClient>());
                interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), ctx);
                return interactionService;
            }
        );
        services.AddHostedService<DiscordBotRunner>();
    }
}