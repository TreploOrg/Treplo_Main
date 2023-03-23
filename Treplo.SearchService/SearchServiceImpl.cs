using Grpc.Core;
using Treplo.SearchService.Helpers;
using Treplo.SearchService.Searching;
using Treplo.SearchService.Searching.Errors;

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
            await track.OnError(static (error, logger) => LogError(logger, error), logger)
                .OnValue(
                    static (value, args) => args.responseStream.WriteAsync(value, args.CancellationToken),
                    (responseStream, context.CancellationToken)
                );
        }
    }

    private static void LogError(ILogger logger, Error error)
    {
        switch (error)
        {
            case ManifestRequestCancelledError manifestRequestCancelledError:
                logger.LogInformation(
                    "Manifest request cancelled for video {VideoId}",
                    manifestRequestCancelledError.VideoId
                );
                break;
            case ManifestRequestError manifestRequestError:
                logger.LogWarning(
                    manifestRequestError.Exception,
                    "Exception occured during manifest processing in video {VideoId}",
                    manifestRequestError.VideoId
                );
                break;
            case NoAudioStreamError noAudioStreamError:
                logger.LogWarning(
                    "There were no audio stream found for video ({VideoTitle} - {VideoId})",
                    noAudioStreamError.Video.Title,
                    noAudioStreamError.Video.Id
                );
                break;
            case SearchCancelledError searchCancelledError:
                logger.LogInformation("Search cancelled for query {Query}", searchCancelledError.Query);
                break;
            case SearchError searchError:
                logger.LogWarning(
                    searchError.Exception,
                    "Exception occured during search for query {Query}",
                    searchError.Query
                );
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}