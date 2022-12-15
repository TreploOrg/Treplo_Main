using Microsoft.Extensions.Options;
using Treplo.Common.Models;
using Treplo.PlayersService.Converters;

namespace Treplo.PlayersService;

public sealed class FfmpegFactory
{
    private readonly IOptions<FfmpegSettings> options;

    public FfmpegFactory(IOptions<FfmpegSettings> options)
    {
        this.options = options;
    }

    public Ffmpeg Create(StreamInfo inStreamInfo, StreamFormatRequest requiredFormat)
    {
        return new Ffmpeg(options.Value.Path, inStreamInfo, requiredFormat);
    }
}

public sealed class FfmpegSettings
{
    public required string Path { get; init; }
}