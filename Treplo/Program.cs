using System.Reflection;
using AutoFixture;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Grpc.Core;
using Grpc.Net.Client.Balancer;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Clustering.Kubernetes;
using Treplo.Common;
using Treplo.Common.OrleansGrpcConnector;
using Treplo.Converters;
using Treplo.Converters.Ffmpeg;
using Treplo.Infrastructure.AspNet;
using Treplo.Infrastructure.Configuration;
using Treplo.Playback;
using Treplo.Playback.Discord;
using Treplo.PlayersService;

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
            .UseOrleans(ConfigureOrleans)
            .ConfigureServices(
                (hostCtx, services) =>
                {
                    services.AddConverters()
                        .AddSingleton<IAudioConverterFactory, FfmpegConverterFactory>();
                        // .AddMongoDBClient(
                        //     "mongodb://treplo:12345678@rc1b-8x8nuotv1zj5ijck.mdb.yandexcloud.net:27018/treplo-db"
                        // );
                    services.AddScoped<IAudioClient, DiscordAudioClient>();
                    services.AddSingleton<IRawAudioSource, RawAudioSource>();
                    SetupDiscordBot(services, hostCtx);
                    services.AddSingleton<IDateTimeManager, DateTimeManager>();
                    services.AddSingleton<ResolverFactory>(
                        sp => new DnsResolverFactory(refreshInterval: TimeSpan.FromSeconds(60)));
                    services.AddGrpcClient<SearchService.SearchService.SearchServiceClient>(
                        static (services, options) =>
                        {
                            var settings = services.GetRequiredService<IOptions<SearchServiceClientSettings>>().Value;
                            options.Address = new Uri(settings.ServiceUrl);
                        }
                    ).ConfigureChannel(
                        x =>
                        {
                            x.ServiceConfig = new ServiceConfig();
                            x.ServiceConfig.LoadBalancingConfigs.Add(new RoundRobinConfig());
                            x.Credentials = ChannelCredentials.Insecure;
                        }
                    );

                    services.AddGrpcClient<PlayersService.PlayersService.PlayersServiceClient>(
                        static (services, options) =>
                        {
                            var settings = services.GetRequiredService<IOptions<PlayerServiceClientSettings>>().Value;
                            options.Address = new Uri(settings.ServiceUrl);
                        }
                    ).ConfigureChannel(
                        x =>
                        {
                            x.ServiceConfig = new ServiceConfig();
                            x.ServiceConfig.LoadBalancingConfigs.Add(new RoundRobinConfig());
                            x.Credentials = ChannelCredentials.Insecure;
                        }
                    );
                }
            )
            .SetupSerilog()
            .ConfigureAppConfiguration(x => x.AddUserSecrets(Assembly.GetExecutingAssembly(), true));

        var host = hostBuilder.Build();

        await host.RunAsync();
    }

    private static void ConfigureOrleans(HostBuilderContext ctx, ISiloBuilder builder)
    {
        if (ctx.HostingEnvironment.IsDevelopment() || true)
        {
            builder.UseLocalhostClustering()
                .AddMemoryGrainStorageAsDefault();
        
            return;
        }
    
        builder
            .UseKubeMembership()
            .AddMongoDBGrainStorageAsDefault(x => x.DatabaseName = "treplo-db")
            .UseKubernetesHosting();
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
        services.AddSingleton<IDiscordClient>(ctx => ctx.GetRequiredService<DiscordSocketClient>());
        services.AddSingleton<InteractionService>();
        services.AddHostedService<DiscordBotRunner>();
    }
}