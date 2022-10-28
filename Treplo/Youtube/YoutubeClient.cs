using System.Runtime.CompilerServices;
using Treplo.Models;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace Treplo.Youtube;

public class YoutubeClient : ISearchClient
{
    private readonly IHttpClientFactory clientFactory;

    public YoutubeClient(IHttpClientFactory clientFactory)
    {
        this.clientFactory = clientFactory;
    }

    public async IAsyncEnumerable<Track> FindAsync(string name,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var result = client.Search.GetVideosAsync(name, cancellationToken);
        await foreach (var video in result.WithCancellation(cancellationToken))
        {
            var duration = video.Duration;
            if (duration is null)
                continue;
            var manifest = await client.Videos.Streams.GetManifestAsync(video.Id, cancellationToken);
            if (manifest.GetAudioOnlyStreams().TryGetWithHighestBitrate() is not IAudioStreamInfo audioStreamInfo)
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

    private YoutubeExplode.YoutubeClient CreateClient()
    {
        var httpClient = clientFactory.CreateClient(nameof(YoutubeExplode));
        return new YoutubeExplode.YoutubeClient(httpClient);
    }
}