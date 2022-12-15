using Treplo.PlayersService.Requests;
using Treplo.PlayersService.Sessions;

namespace Treplo.PlayersService.Handlers;

public sealed class SearchRespondRequestHandler : PerSessionRequestBaseHandler<SearchRespondRequest>
{
    private readonly PlayerSessionsManager sessionsManager;

    public SearchRespondRequestHandler(PlayerSessionsManager sessionsManager, ILogger<SearchRespondRequestHandler> logger) : base(logger)
    {
        this.sessionsManager = sessionsManager;
    }
    
    protected override async ValueTask<IResult> HandleInternal(SearchRespondRequest request, CancellationToken cancellationToken)
    {
        await sessionsManager.RespondToSearchAsync(request.SessionId, request.SearchSessionId, request.SearchResultIndex);
        return Results.Ok();
    }
}