using System.Runtime.CompilerServices;
using Google.Protobuf.WellKnownTypes;
using SimpleResult;
using SimpleResult.Extensions;
using Treplo.Common;
using Treplo.SearchService.Searching.Errors;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

// using YoutubeExplorer;
// using YoutubeExplorer.Common;
// using YoutubeExplorer.Search;
// using YoutubeExplorer.Videos;
// using YoutubeExplorer.Videos.Streams;

namespace Treplo.SearchService.Searching.Youtube;

public class YoutubeEngine : ISearchEngine
{
    private readonly IHttpClientFactory clientFactory;

    public YoutubeEngine(IHttpClientFactory clientFactory)
    {
        this.clientFactory = clientFactory;
    }

    public string Name => "Youtube";

    async IAsyncEnumerable<Result<Track, Error>> ISearchEngine.FindInternalAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var httpClient = clientFactory.CreateClient(nameof(YoutubeEngine));
        var youtubeClient = new YoutubeClient(httpClient);
        var batches = youtubeClient.Search.GetResultBatchesAsync(query, SearchFilter.Video, cancellationToken);
        await using var enumerator = batches.GetAsyncEnumerator(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var moveResult = await MoveNext(enumerator, query);
            if (moveResult is not { } result)
                yield break;

            if (result.IsError)
            {
                yield return result.UnwrapError();
                yield break;
            }

            var manifestTasks = result.UnwrapOrDefault()!.OfType<VideoSearchResult>().Select(
                async video =>
                {
                    var manifest = await GetManifest(youtubeClient.Videos.Streams, video.Id, cancellationToken);
                    return (video, manifest);
                }
            );

            var manifests = await Task.WhenAll(manifestTasks);

            foreach (var (video, manifestResult) in manifests)
            {
                if (manifestResult is not { } manifest)
                    continue;
                yield return manifest.AndThen(
                    static (manifest, video) => CollectTrack(video, manifest).MapError(static error => (Error)error),
                    video
                );
            }
        }
    }

    private static async ValueTask<Result<IReadOnlyList<ISearchResult>, Error>?> MoveNext(
        IAsyncEnumerator<Batch<ISearchResult>> enumerator,
        string query
    )
    {
        try
        {
            if (await enumerator.MoveNextAsync())
                return Result.Ok<IReadOnlyList<ISearchResult>, Error>(enumerator.Current.Items);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            return Error.ErrorInSearch(query, e);
        }

        return null;
    }

    private static async Task<Result<StreamManifest, Error>?> GetManifest(
        StreamClient client,
        VideoId id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await client.GetManifestAsync(id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            return Error.ErrorInManifest(id, e);
        }

        return null;
    }

    private static Result<Track, NoAudioStreamError> CollectTrack(VideoSearchResult video, StreamManifest manifest)
    {
        var audioStreamInfo = manifest
            .GetAudioOnlyStreams()
            .TryGetWithHighestBitrate() as IAudioStreamInfo;
        if (audioStreamInfo is null)
            return Error.NoAudioStream(video);
        return new Track
        {
            Author = video.Author.ChannelTitle,
            Title = video.Title,
            Thumbnail = video.Thumbnails.GetWithHighestResolution().Into(),
            Source = audioStreamInfo.Into(),
            Duration = Duration.FromTimeSpan(video.Duration.GetValueOrDefault()),
        };
    }
}