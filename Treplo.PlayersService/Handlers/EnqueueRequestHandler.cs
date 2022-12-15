using Treplo.PlayersService.Requests;
using Treplo.PlayersService.Sessions;

namespace Treplo.PlayersService.Handlers;

public class EnqueueRequestHandler : PerSessionRequestBaseHandler<EnqueueRequest>
{
    private readonly PlayerSessionsManager playerSessionsManager;

    public EnqueueRequestHandler(PlayerSessionsManager playerSessionsManager,ILogger<EnqueueRequestHandler> logger) : base(logger)
    {
        this.playerSessionsManager = playerSessionsManager;
    }

    protected override async ValueTask<IResult> HandleInternal(EnqueueRequest request, CancellationToken cancellationToken)
    {
        await playerSessionsManager.EnqueueAsync(request.SessionId, request.Track);
        return Results.Ok();
    }
}