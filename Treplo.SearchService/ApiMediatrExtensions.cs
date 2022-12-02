using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Treplo.SearchService;

public static class ApiMediatrExtensions
{
    public static WebApplication MapGet<TRequest>(this WebApplication app, string template)
        where TRequest : IHttpRequest
    {
        app.MapGet(template,
            async ([AsParameters] TRequest request, IMediator mediatr, CancellationToken cancellationToken) =>
                await mediatr.Send(request, cancellationToken));
        return app;
    }

    public static WebApplication MapGetStream<TRequest, TItem>(this WebApplication app, string template)
        where TRequest : IHttpStreamRequest<TItem>
    {
        
        return app;
    }
}