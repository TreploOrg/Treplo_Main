using MediatR;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;
using Treplo.SearchService;
using Treplo.SearchService.Helpers;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.AddMediatr()
    .AddYoutubeEngine()
    .AddSearchEngineManager();

builder.Host.UseSerilog((ctx, config)
    => config.ReadFrom.Configuration(ctx.Configuration)
);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.MapGet("/search",
    ([FromServices] IMediator mediatr, [AsParameters] SearchRequest request, CancellationToken cancellationToken) =>
        mediatr.CreateStream(request, cancellationToken)
);

app.Run();