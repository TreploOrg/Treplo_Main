using System.IO.Pipelines;
using System.Text.RegularExpressions;
using MediatR;
using Treplo.PlayersService.Converters;
using Treplo.PlayersService.Requests;
using Treplo.PlayersService.Sessions;

namespace Treplo.PlayersService.Handlers;

public sealed class PlayRequestHandler : PerSessionRequestBaseHandler<PlayRequest>
{
    private readonly PlayerSessionsManager playerSessionsManager;

    public PlayRequestHandler(PlayerSessionsManager playerSessionsManager, ILogger<PlayRequestHandler> logger) : base(logger)
    {
        this.playerSessionsManager = playerSessionsManager;
    }

    protected override async ValueTask<IResult> HandleInternal(PlayRequest request, CancellationToken cancellationToken)
    {
        var pipeReader = await playerSessionsManager.PlayAsync(request.SessionId);
        return pipeReader is null ? Results.Ok() : Results.Stream(pipeReader);
    }
}