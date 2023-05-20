using Treplo.Common;
using Yandex.Music.Api.Models.Artist;
using Yandex.Music.Api.Models.Common;
using Bitrate = Treplo.Common.Bitrate;
using Container = Treplo.Common.Container;

namespace Treplo.SearchService.Searching.Yandex.Music;

public static class YandexTracksExtensions
{
    public static AudioSource ToAudioSource(this YTrackDownloadInfo downloadInfo, string fileLink, long fileSize) => new()
    {
        Url = fileLink,
        Bitrate = downloadInfo.BitrateInKbps.ToBitrate(),
        Codec = downloadInfo.Codec.ToCodec(),
        Container = nameof(YandexMusicEngine).ToContainer(), // что сюда нужно????
        Filesize = fileSize.ToFileSize(),
    };

    private static Codec ToCodec(this string codec) => new()
    {
        Name = codec,
    };

    private static Container ToContainer(this string name) => new()
    {
        Name = name,
    };


    public static Thumbnail ToThumbnail(this string uri) => new()
    {
        Url = uri,
    };

    private static Bitrate ToBitrate(this int bitrateInKbps) => new()
    {
        BitsPerSecond = (ulong)bitrateInKbps * 1000,
    };

    private static Filesize ToFileSize(this long fileSize) => new()
    {
        BytesLength = (ulong)fileSize,
    };

    internal static string GetArtistsNames(this IEnumerable<YArtist> artists)
    {
        return string.Join(", ", artists.Select( art => art.Name));
    }
}