﻿using System.Reflection;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Treplo.Common.OrleansGrpcConnector;
using Treplo.Infrastructure.AspNet;
using Treplo.Infrastructure.Configuration;

namespace Treplo;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .BindOption<FfmpegSettings>()
            .BindOption<SearchServiceClientSettings>()
            .BindOption<PlayerServiceClientSettings>()
            .BindOption<DiscordClientSettings>()
            .UseOrleans(
                siloBuilder => siloBuilder.UseLocalhostClustering()
                    .AddMemoryGrainStorageAsDefault()
            )
            .ConfigureServices(
                (hostCtx, services) =>
                {
                    services.AddConverters()
                        .AddSingleton<FfmpegFactory>();
                    SetupDiscordBot(services, hostCtx);
                    services.AddSingleton<IDateTimeManager, DateTimeManager>();

                    services.AddGrpcClient<SearchService.SearchService.SearchServiceClient>(
                        static (services, options) =>
                        {
                            var settings = services.GetRequiredService<IOptions<SearchServiceClientSettings>>().Value;
                            options.Address = new Uri(settings.ServiceUrl);
                        }
                    );

                    services.AddGrpcClient<PlayersService.PlayersService.PlayersServiceClient>(
                        static (services, options) =>
                        {
                            var settings = services.GetRequiredService<IOptions<PlayerServiceClientSettings>>().Value;
                            options.Address = new Uri(settings.ServiceUrl);
                        }
                    );
                }
            )
            .SetupSerilog()
            .ConfigureAppConfiguration(x => x.AddUserSecrets(Assembly.GetExecutingAssembly(), true));

        var host = hostBuilder.Build();

        await host.RunAsync();
    }

    private static void SetupDiscordBot(IServiceCollection services, HostBuilderContext hostCtx)
    {
        services.AddSingleton(
            ctx =>
            {
                var settings = ctx.GetRequiredService<IOptions<DiscordClientSettings>>();
                var client = new DiscordSocketClient(
                    new DiscordSocketConfig
                    {
                        GatewayIntents = settings.Value.Intents
                            .Aggregate(
                                GatewayIntents.None,
                                (seed, cur) => seed | cur
                            ),
                    }
                );

                return client;
            }
        );
        services.AddSingleton(
            ctx =>
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