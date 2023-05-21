using Treplo.Common;
using YoutubeExplode.Videos.Streams;
//using YoutubeExplorer.Videos.Streams;
using Bitrate = Treplo.Common.Bitrate;
using Container = Treplo.Common.Container;

namespace Treplo.SearchService.Searching.Youtube;

public static class ModelsExtensions
{
    public static AudioSource Into(this IAudioStreamInfo streamInfo) => new()
    {
        Url = streamInfo.Url,
        Bitrate = streamInfo.Bitrate.Into(),
        Codec = streamInfo.AudioCodec.Into(),
        Container = streamInfo.Container.Into(),
        Filesize = streamInfo.Size.Into(),
    };

    public static Bitrate Into(in this YoutubeExplode.Videos.Streams.Bitrate bitrate) => new()
    {
        BitsPerSecond = (ulong)bitrate.BitsPerSecond,
    };

    public static Thumbnail Into(this YoutubeExplode.Common.Thumbnail thumbnail) => new()
    {
        Url = thumbnail.Url,
    };

    public static Filesize Into(in this FileSize fileSize) => new()
    {
        BytesLength = (ulong)fileSize.Bytes,
    };

    public static Container Into(in this YoutubeExplode.Videos.Streams.Container container) => new()
    {
        Name = container.Name,
    };

    public static Codec Into(this string codec) => new()
    {
        Name = codec,
    };
}