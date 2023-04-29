using Microsoft.AspNetCore.Server.Kestrel.Core;
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
builder.Host.UseOrleans(
    x
        => x.UseLocalhostClustering()
            .AddMemoryGrainStorageAsDefault()
);

var app = builder.Build();
app.SetupSwaggerEndpoints()
    .UseSerilogRequestLogging();

app.MapGrpcService<PlayersServiceImpl>();

if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

await app.RunAsync();