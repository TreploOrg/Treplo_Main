using YoutubeExplorer.Common;
using YoutubeExplorer.Videos.Streams;

namespace Treplo.Youtube;

public static class ModelsExtensions
{
    public static Models.StreamInfo Into(this IAudioStreamInfo streamInfo) => new()
    {
        Url = streamInfo.Url,
        Bitrate = streamInfo.Bitrate.Into(),
        Codec = streamInfo.AudioCodec.Into(),
        Container = streamInfo.Container.Into(),
        Size = streamInfo.Size.Into(),
    };
    public static Models.Bitrate Into(in this Bitrate bitrate) => new(bitrate.BitsPerSecond);
    public static Models.Thumbnail Into(this Thumbnail thumbnail) => new(thumbnail.Url);
    public static Models.FileSize Into(in this FileSize fileSize) => new(fileSize.Bytes);
    public static Models.Container Into(in this Container container) => new(container.Name);
    public static Models.Codec Into(this string codec) => new(codec);
}