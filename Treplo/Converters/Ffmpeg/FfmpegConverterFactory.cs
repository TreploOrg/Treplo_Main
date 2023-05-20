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

    IAudioConverter IAudioConverterFactory.Create(AudioSource audioSource, in StreamFormatRequest requiredFormat, TimeSpan? startTime)
        => Create(audioSource, in requiredFormat, startTime);

    public FfmpegConverter Create(AudioSource audioSource, in StreamFormatRequest requiredFormat, TimeSpan? startTime)
        => new(options.Value.Path, audioSource, in requiredFormat, startTime);
}