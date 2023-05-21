// using YoutubeExplorer.Search;
// using YoutubeExplorer.Videos;

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