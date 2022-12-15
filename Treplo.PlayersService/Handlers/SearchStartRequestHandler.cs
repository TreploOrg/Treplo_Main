using Treplo.PlayersService.Requests;
using Treplo.PlayersService.Sessions;

namespace Treplo.PlayersService.Handlers;

public sealed class StartSearchRequestHandler : PerSessionRequestBaseHandler<SearchStartRequest>
{
    private readonly PlayerSessionsManager sessionsManager;

    public StartSearchRequestHandler(PlayerSessionsManager sessionsManager, ILogger<StartSearchRequestHandler> logger) : base(logger)
    {
        this.sessionsManager = sessionsManager;
    }

    protected override async ValueTask<IResult> HandleInternal(
        SearchStartRequest request,
        CancellationToken cancellationToken
    )
    {
        var searchId = await sessionsManager.StartSearchAsync(request.SessionId, request.SearchTracks);
        return Results.Ok(searchId);
    }
}