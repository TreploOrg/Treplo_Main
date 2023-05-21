using Microsoft.AspNetCore.Server.Kestrel.Core;
using Orleans.Clustering.Kubernetes;
using Orleans.Configuration;
using Serilog;
using Treplo.Common.OrleansGrpcConnector;
using Treplo.Infrastructure.AspNet;
using Treplo.PlayersService;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2));
builder.Host.SetupSerilog();
builder.Services
    .SetupSwaggerAndOpenApi()
    .AddGrpc().Services
    .AddSingleton<PlayersServiceImpl>()
    .AddGrpcReflection()
    .AddHttpClient()
    .AddConverters();
    //.AddMongoDBClient("mongodb://treplo:12345678@rc1b-8x8nuotv1zj5ijck.mdb.yandexcloud.net:27018/treplo-db");

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