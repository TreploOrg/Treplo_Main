using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Treplo.Infrastructure.AspNet;
using Treplo.SearchService;
using Treplo.SearchService.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2));
builder.Host.SetupSerilog();
builder.Services
    .SetupSwaggerAndOpenApi()
    .AddMediatr(Assembly.GetExecutingAssembly())
    .AddYoutubeEngine()
    .AddSearchEngineManager()
    .AddGrpc().Services
    .AddSingleton<SearchServiceImpl>()
    .AddGrpcReflection()
    .AddHttpClient();

var app = builder.Build();

app.SetupSwaggerEndpoints()
    .UseSerilogRequestLogging();

app.MapGrpcService<SearchServiceImpl>();

if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

await app.RunAsync();