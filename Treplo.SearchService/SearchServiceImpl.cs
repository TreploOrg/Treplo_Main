using Grpc.Core;
using Treplo.SearchService.Helpers;
using Treplo.SearchService.Searching;

namespace Treplo.SearchService;

public sealed class SearchServiceImpl : SearchService.SearchServiceBase
{
    private readonly ISearchEngineManager engineManager;
    private readonly ILogger<SearchServiceImpl> logger;

    public SearchServiceImpl(ISearchEngineManager engineManager, ILogger<SearchServiceImpl> logger)
    {
        this.engineManager = engineManager;
        this.logger = logger;
    }

    public override async Task Search(
        TrackSearchRequest request,
        IServerStreamWriter<TrackSearchResult> responseStream,
        ServerCallContext context
    )
    {
        logger.LogInformation("Started search for query {Query}. Limit: {Limit}", request.Query, request.Limit);
        var tracks = engineManager.SearchAsync(request.Query, context.CancellationToken);
        if (request.HasLimit)
            tracks = tracks.Take(request.Limit, context.CancellationToken);
        await foreach (var track in tracks)
        {
            await responseStream.WriteAsync(track, context.CancellationToken);
        }
    }
}