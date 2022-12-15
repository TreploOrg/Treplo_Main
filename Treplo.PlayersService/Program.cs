using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Treplo.Infrastructure;
using Treplo.Infrastructure.AspNet;
using Treplo.PlayersService;
using Treplo.PlayersService.Handlers;
using Treplo.PlayersService.Requests;
using Treplo.PlayersService.Sessions;

var builder = WebApplication.CreateBuilder(args);

builder.SetupSerilog()
    .SetupSwaggerAndOpenApi()
    .AddMediatr();
builder.Host.BindOption<FfmpegSettings>();
builder.Services.AddHttpClient<PlayerSessionsManager>();
builder.Services.AddSingleton<FfmpegFactory>();
builder.Services.AddSingleton<PlayerSessionsManager>();
var app = builder.Build();

app.SetupSwaggerEndpoints();

app.MapPost("/play",
    async (
        [FromServices] IMediator mediator,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)]
        PlayRequest request,
        CancellationToken cancellationToken
    ) => await mediator.Send(request, cancellationToken)
);

app.MapPost("/enqueue",
    async (
        [FromServices] IMediator mediator,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)]
        EnqueueRequest request,
            CancellationToken cancellationToken
        ) => await mediator.Send(request, cancellationToken)
    );

app.MapPost("/search-start",
    async (
        [FromServices] IMediator mediator,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)]
        SearchStartRequest request,
        CancellationToken cancellationToken
    ) => await mediator.Send(request, cancellationToken)
);

app.MapPost("/search-respond",
    async (
        [FromServices] IMediator mediator,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] 
        SearchRespondRequest request,
        CancellationToken cancellationToken
    ) => await mediator.Send(request, cancellationToken)
);

app.Run();