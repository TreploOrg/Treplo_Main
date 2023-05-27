using YoutubeExplode.Search;
using YoutubeExplode.Videos;

namespace Treplo.SearchService.Searching.Errors;

public abstract class Error
{
    public static SearchError ErrorInSearch(string query, Exception exception) => new(query, exception);

    public static ManifestRequestError ErrorInManifest(VideoId videoId, Exception exception) => new(videoId, exception);

    public static NoAudioStreamError NoAudioStream(VideoSearchResult video) => new(video);
}

public sealed class NoAudioStreamError : Error
{
    public NoAudioStreamError(VideoSearchResult video)
    {
        Video = video;
    }

    public VideoSearchResult Video { get; }
}

public sealed class ManifestRequestError : Error
{
    public ManifestRequestError(VideoId videoId, Exception exception)
    {
        VideoId = videoId;
        Exception = exception;
    }

    public VideoId VideoId { get; }
    public Exception Exception { get; }
}

public sealed class SearchError : Error
{
    public SearchError(string query, Exception exception)
    {
        Query = query;
        Exception = exception;
    }

    public string Query { get; }
    public Exception Exception { get; }
}