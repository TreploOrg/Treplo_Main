using System.Reflection;
using Serilog;
using Treplo.Infrastructure.AspNet;
using Treplo.SearchService;
using Treplo.SearchService.Helpers;

var builder = WebApplication.CreateBuilder(args);

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