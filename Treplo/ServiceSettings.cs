using Treplo.Infrastructure.Configuration;

namespace Treplo;

public abstract class GrpcServiceSettings
{
    public required string ServiceUrl { get; init; }
}

public sealed class SearchServiceClientSettings : GrpcServiceSettings, ISetting
{
    public static string SectionName => nameof(SearchServiceClientSettings);
}

public sealed class PlayerServiceClientSettings : GrpcServiceSettings, ISetting
{
    public static string SectionName => nameof(PlayerServiceClientSettings);
}