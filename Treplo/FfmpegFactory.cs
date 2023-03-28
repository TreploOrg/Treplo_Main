using Microsoft.Extensions.Options;
using Treplo.Common;
using Treplo.Converters;

namespace Treplo;

public sealed class FfmpegFactory
{
    private readonly IOptions<FfmpegSettings> options;

    public FfmpegFactory(IOptions<FfmpegSettings> options)
    {
        this.options = options;
    }

    public Ffmpeg Create(AudioSource audioSource, in StreamFormatRequest requiredFormat)
    {
        return new Ffmpeg(options.Value.Path, audioSource, in requiredFormat);
    }
}