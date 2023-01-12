using Microsoft.Extensions.Options;
using Treplo.Common.Models;
using Treplo.Infrastructure.Configuration;
using Treplo.PlayersService.Converters;

namespace Treplo.PlayersService;

public sealed class FfmpegFactory
{
    private readonly IOptions<FfmpegSettings> options;

    public FfmpegFactory(IOptions<FfmpegSettings> options)
    {
        this.options = options;
    }

    public Ffmpeg Create(in StreamInfo inStreamInfo, in StreamFormatRequest requiredFormat)
    {
        return new Ffmpeg(options.Value.Path, in inStreamInfo, in requiredFormat);
    }
}

public sealed class FfmpegSettings : ISetting
{
    public required string Path { get; init; }

    public static string SectionName => nameof(FfmpegSettings);
}