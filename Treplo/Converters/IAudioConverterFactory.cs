using Treplo.Common;

namespace Treplo.Converters;

public interface IAudioConverterFactory
{
    IAudioConverter Create(AudioSource audioSource, in StreamFormatRequest requiredFormat);
}