using Microsoft.Extensions.Options;
using Treplo.Common;

namespace Treplo.Converters.Ffmpeg;

public sealed class FfmpegConverterFactory : IAudioConverterFactory
{
    private readonly IOptions<FfmpegSettings> options;

    public FfmpegConverterFactory(IOptions<FfmpegSettings> options)
    {
        this.options = options;
    }

    public FfmpegConverter Create(AudioSource audioSource, in StreamFormatRequest requiredFormat)
        => new(options.Value.Path, audioSource, in requiredFormat);

    IAudioConverter IAudioConverterFactory.Create(AudioSource audioSource, in StreamFormatRequest requiredFormat)
        => Create(audioSource, in requiredFormat);
}