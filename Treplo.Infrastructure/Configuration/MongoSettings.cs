namespace Treplo.Infrastructure.Configuration;

public sealed class MongoSettings : ISetting
{
    public required string ConnectionString { get; init; }
    public static string SectionName => nameof(MongoSettings);
}