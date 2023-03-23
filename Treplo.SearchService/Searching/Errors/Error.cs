using YoutubeExplorer.Search;
using YoutubeExplorer.Videos;

namespace Treplo.SearchService.Searching.Errors;

public abstract class Error
{
    public static SearchError ErrorInSearch(string query, Exception exception) => new(query, exception);

    public static ManifestRequestCancelledError ManifestCanceled(VideoId videoId) => new(videoId);

    public static ManifestRequestError ErrorInManifest(VideoId videoId, Exception exception) => new(videoId, exception);

    public static NoAudioStreamError NoAudioStream(VideoSearchResult video) => new(video);

    public static SearchCancelledError SearchCancelled(string query) => new(query);
}

public sealed class SearchCancelledError : Error
{
    public string Query { get; }

    public SearchCancelledError(string query)
    {
        Query = query;
    }
}

public sealed class NoAudioStreamError : Error
{
    public VideoSearchResult Video { get; }

    public NoAudioStreamError(VideoSearchResult video)
    {
        Video = video;
    }
}

public sealed class ManifestRequestError : Error
{
    public VideoId VideoId { get; }
    public Exception Exception { get; }

    public ManifestRequestError(VideoId videoId, Exception exception)
    {
        VideoId = videoId;
        Exception = exception;
    }
}

public sealed class ManifestRequestCancelledError : Error
{
    public VideoId VideoId { get; }

    public ManifestRequestCancelledError(VideoId videoId)
    {
        VideoId = videoId;
    }
}

public sealed class SearchError : Error
{
    public string Query { get; }
    public Exception Exception { get; }

    public SearchError(string query, Exception exception)
    {
        Query = query;
        Exception = exception;
    }
}