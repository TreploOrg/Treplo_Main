using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orleans.Clustering.Kubernetes;
using Serilog;
using Treplo.Common.OrleansGrpcConnector;
using Treplo.Infrastructure.AspNet;
using Treplo.Infrastructure.Configuration;
using Treplo.PlayersService;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2));
builder.Host.SetupSerilog()
    .BindOption<MongoSettings>();
builder.Services
    .SetupSwaggerAndOpenApi()
    .AddGrpc().Services
    .AddSingleton<PlayersServiceImpl>()
    .AddGrpcReflection()
    .AddHttpClient()
    .AddHttpClient(nameof(PlayersServiceImpl)).ConfigureHttpMessageHandlerBuilder(
        x =>
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false,
            };

            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            handler.CookieContainer.Add(new Cookie("CONSENT", "YES+cb", "/", ".youtube.com"));
            x.PrimaryHandler = handler;
        }
    ).ConfigureHttpClient(
        x => x.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.111 Safari/537.36"
        )
    ).Services
    .AddConverters();

if (!builder.Environment.IsDevelopment())
    builder.Services.AddMongoDBClient(
        x => MongoClientSettings.FromConnectionString(
            x.GetRequiredService<IOptions<MongoSettings>>().Value.ConnectionString
        )
    );

builder.Host.UseOrleans(ConfigureOrleans);

var app = builder.Build();
app.SetupSwaggerEndpoints()
    .UseSerilogRequestLogging();

app.MapGrpcService<PlayersServiceImpl>();

if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

await app.RunAsync();

static void ConfigureOrleans(HostBuilderContext ctx, ISiloBuilder builder)
{
    if (ctx.HostingEnvironment.IsDevelopment())
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