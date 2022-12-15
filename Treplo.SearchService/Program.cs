using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Treplo.Infrastructure.AspNet;
using Treplo.SearchService;
using Treplo.SearchService.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.SetupSerilog()
    .SetupSwaggerAndOpenApi()
    .AddMediatr(Assembly.GetExecutingAssembly())
    .AddYoutubeEngine()
    .AddSearchEngineManager();

var app = builder.Build();

app.SetupSwaggerEndpoints()
    .UseSerilogRequestLogging();

app.MapGet("/search",
    ([FromServices] IMediator mediatr, [AsParameters] SearchRequest request, CancellationToken cancellationToken) =>
        mediatr.CreateStream(request, cancellationToken)
);

app.Run();