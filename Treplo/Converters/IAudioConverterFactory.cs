using Treplo.Common;
using Treplo.Converters.Ffmpeg;

namespace Treplo.Converters;

public interface IAudioConverterFactory
{
    IAudioConverter Create(AudioSource audioSource, in StreamFormatRequest requiredFormat);
}