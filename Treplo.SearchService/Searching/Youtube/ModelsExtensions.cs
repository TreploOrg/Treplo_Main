using Treplo.Common.Models;
using YoutubeExplorer.Videos.Streams;
using Bitrate = Treplo.Common.Models.Bitrate;
using Container = Treplo.Common.Models.Container;
using FileSize = Treplo.Common.Models.FileSize;

namespace Treplo.SearchService.Searching.Youtube;

public static class ModelsExtensions
{
    public static StreamInfo Into(this IAudioStreamInfo streamInfo) => new()
    {
        Url = streamInfo.Url,
        Bitrate = streamInfo.Bitrate.Into(),
        Codec = streamInfo.AudioCodec.Into(),
        Container = streamInfo.Container.Into(),
        Size = streamInfo.Size.Into(),
    };

    public static Bitrate Into(in this YoutubeExplorer.Videos.Streams.Bitrate bitrate) => new(bitrate.BitsPerSecond);

    public static Thumbnail Into(this YoutubeExplorer.Common.Thumbnail thumbnail) => new(thumbnail.Url);

    public static FileSize Into(in this YoutubeExplorer.Videos.Streams.FileSize fileSize) => new(fileSize.Bytes);

    public static Container Into(in this YoutubeExplorer.Videos.Streams.Container container) => new(container.Name);

    public static Codec Into(this string codec) => new(codec);
}