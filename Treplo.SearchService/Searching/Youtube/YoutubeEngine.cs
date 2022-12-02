using System.Runtime.CompilerServices;
using Treplo.Common.Models;
using YoutubeExplorer;
using YoutubeExplorer.Common;
using YoutubeExplorer.Search;
using YoutubeExplorer.Videos.Streams;

namespace Treplo.SearchService.Searching.Youtube;

public class YoutubeEngine : ISearchEngine
{
    private readonly IHttpClientFactory clientFactory;

    public YoutubeEngine(IHttpClientFactory clientFactory)
    {
        this.clientFactory = clientFactory;
    }

    async IAsyncEnumerable<Track> ISearchEngine.FindInternalAsync(string query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var httpClient = clientFactory.CreateClient(nameof(YoutubeEngine));
        var youtubeClient = new YoutubeClient(httpClient);
        var batches = youtubeClient.Search.GetResultBatchesAsync(query, SearchFilter.Video, cancellationToken);
        await foreach (var batch in batches.WithCancellation(cancellationToken))
        {
            var videos = batch.Items
                .OfType<VideoSearchResult>()
                .Where(x => x.Duration is not null)
                .ToArray();
            var manifestTasks = videos
                .Select(x => youtubeClient.Videos.Streams.GetManifestAsync(x.Id, cancellationToken).AsTask());

            var manifests = await Task.WhenAll(manifestTasks);

            foreach (var track in videos.Zip(manifests, CollectTrack).Where(track => track is not null))
            {
                yield return track.GetValueOrDefault();
            }
        }
    }

    private static Track? CollectTrack(VideoSearchResult video, StreamManifest manifest)
    {
        var audioStreamInfo = manifest
            .GetAudioOnlyStreams()
            .TryGetWithHighestBitrate() as IAudioStreamInfo;
        if (audioStreamInfo is null)
            return null;
        return new Track
        {
            Author = video.Author.ChannelTitle,
            Title = video.Title,
            Thumbnail = video.Thumbnails.GetWithHighestResolution().Into(),
            Source = audioStreamInfo.Into(),
            Duration = video.Duration.GetValueOrDefault(),
        };
    }

    public string Name => "Youtube";
}