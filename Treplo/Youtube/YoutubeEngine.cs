using System.Runtime.CompilerServices;
using Treplo.Models;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace Treplo.Youtube;

public class YoutubeEngine : ISearchEngine
{
    private readonly IHttpClientFactory clientFactory;

    public YoutubeEngine(IHttpClientFactory clientFactory)
    {
        this.clientFactory = clientFactory;
    }
    
    async IAsyncEnumerable<Track> ISearchEngine.FindInternalAsync(string query, [EnumeratorCancellation]CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var result = client.Search.GetVideosAsync(query, cancellationToken);
        await foreach (var video in result.WithCancellation(cancellationToken))
        {
            var duration = video.Duration;
            if (duration is null)
                continue;
            var manifest = await client.Videos.Streams.GetManifestAsync(video.Id, cancellationToken);
            var audioStreamInfo = manifest
                .GetAudioOnlyStreams()
                .Where(x => x.Container.Name != "mp4")
                .TryGetWithHighestBitrate() as IAudioStreamInfo;
            if (audioStreamInfo is null)
                continue;
            yield return new Track
            {
                Author = video.Author.ChannelTitle,
                Title = video.Title,
                Thumbnail = video.Thumbnails.GetWithHighestResolution().Into(),
                Source = audioStreamInfo.Into(),
                Duration = duration.GetValueOrDefault(),
            };
        }
    }

    public string Name => "Youtube";

    private YoutubeExplode.YoutubeClient CreateClient()
    {
        var httpClient = clientFactory.CreateClient(nameof(YoutubeExplode));
        return new YoutubeExplode.YoutubeClient(httpClient);
    }
}