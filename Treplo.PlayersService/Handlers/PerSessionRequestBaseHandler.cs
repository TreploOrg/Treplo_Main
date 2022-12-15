using System.Diagnostics;
using MediatR;
using Treplo.PlayersService.Requests;

namespace Treplo.PlayersService.Handlers;

public abstract class PerSessionRequestBaseHandler<TRequest> : IRequestHandler<TRequest, IResult> where TRequest : PerSessionRequest
{
    protected readonly ILogger<PerSessionRequestBaseHandler<TRequest>> Logger;

    protected PerSessionRequestBaseHandler(ILogger<PerSessionRequestBaseHandler<TRequest>> logger)
    {
        Logger = logger;
    }
    
    public async Task<IResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Handling {RequestType} for session {SessionId}", typeof(TRequest), request.SessionId);
        var start = Stopwatch.GetTimestamp();
        var result = await HandleInternal(request, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(start);
        Logger.LogInformation("Finished handling {RequestType} for session {SessionId} in {Elapsed}", typeof(TRequest), request.SessionId, elapsed);
        return result;
    }

    protected abstract ValueTask<IResult> HandleInternal(TRequest request, CancellationToken cancellationToken);
}