using MediatR;
using Treplo.Common.Models;
using Treplo.SearchService.Helpers;
using Treplo.SearchService.Searching;

namespace Treplo.SearchService.Handlers;

public class SearchRequestHandler : IStreamRequestHandler<SearchRequest, TrackSearchResult>
{
    private readonly ILogger<SearchRequestHandler> logger;
    private readonly ISearchEngineManager searchEngineManager;

    public SearchRequestHandler(ISearchEngineManager searchEngineManager, ILogger<SearchRequestHandler> logger)
    {
        this.searchEngineManager = searchEngineManager;
        this.logger = logger;
    }

    public IAsyncEnumerable<TrackSearchResult> Handle(SearchRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Started search for query {Query}. Limit: {Limit}", request.Query, request.Limit);
        var searchResult = searchEngineManager.SearchAsync(request.Query, cancellationToken);
        if (request.Limit is { } limit)
            searchResult = searchResult.Take(limit, cancellationToken);

        return searchResult;
    }
}