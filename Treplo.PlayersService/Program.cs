using MediatR;
using Microsoft.AspNetCore.Mvc;
using Treplo.Infrastructure;
using Treplo.Infrastructure.AspNet;
using Treplo.PlayersService;
using Treplo.PlayersService.Playback;

var builder = WebApplication.CreateBuilder(args);

builder.SetupSerilog()
    .SetupSwaggerAndOpenApi()
    .AddMediatr();
builder.Host.BindOption<FfmpegSettings>();
builder.Host.UseOrleans(x
    => x.UseLocalhostClustering()
        .AddMemoryGrainStorageAsDefault()
);
builder.Services.AddSingleton<FfmpegFactory>()
    .AddHttpClient();

var app = builder.Build();
app.SetupSwaggerEndpoints();

app.MapPost("/play",
    async (
            [FromServices] IMediator mediator, 
            [FromBody] PlayRequest request, 
            CancellationToken cancellationToken)
        => await mediator.Send(request, cancellationToken)
        );

app.Run();