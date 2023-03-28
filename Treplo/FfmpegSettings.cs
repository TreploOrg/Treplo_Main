using Treplo.Infrastructure.Configuration;

namespace Treplo;

public sealed class FfmpegSettings : ISetting
{
    public required string Path { get; init; }

    public static string SectionName => nameof(FfmpegSettings);
}