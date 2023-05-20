using System.Runtime.CompilerServices;
using Google.Protobuf.WellKnownTypes;
using SimpleResult;
using Treplo.Common;
using Treplo.SearchService.Searching.Errors;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Track;

namespace Treplo.SearchService.Searching.Yandex.Music;

public class YandexMusicEngine : ISearchEngine
{
    private readonly AuthStorage authStorage;
    private readonly YandexMusicApi yandexMusicApi;

    public YandexMusicEngine(AuthStorage authStorage, YandexMusicApi yandexMusicApi)
    {
        this.authStorage = authStorage;
        this.yandexMusicApi = yandexMusicApi;
    }

    public string Name => "YandexMusicEngine";

    public async IAsyncEnumerable<Result<Track, Error>> FindInternalAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var tracks = (await yandexMusicApi.Search.SearchAsync(authStorage, query, YSearchType.Track)).Result.Tracks
            .Results;
        using var enumerator = tracks.GetEnumerator();
        while (enumerator.MoveNext() && !cancellationToken.IsCancellationRequested)
        {
            if (enumerator.Current == null)
                yield break;
            var result = await MapToGrpcTrack(enumerator.Current);
            if (result.IsError)
            {
                yield return result;
                yield break;
            }

            yield return result;
        }
    }

    public async Task<Result<Track, Error>> MapToGrpcTrack(YTrack track)
    {
        if (track == null)
            throw new ArgumentNullException(nameof(track));
        var metadata = await yandexMusicApi.Track.GetMetadataForDownloadAsync(authStorage, track);
        var metadataForDownload = metadata.Result.First();
        var fileLink = await yandexMusicApi.Track.GetFileLinkAsync(authStorage, track);
        var createdTrack = new Track
        {
            Author = track.Artists.GetArtistsNames(),
            Title = track.Title,
            Thumbnail = track.CoverUri.ToThumbnail(),
            Source = metadataForDownload.ToAudioSource(fileLink, track.FileSize),
            Duration = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(track.DurationMs)),
        };
        return Result<Track, Error>.Ok(createdTrack);
    }
}