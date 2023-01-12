using Discord;
using Treplo.Infrastructure.Configuration;

namespace Treplo;

public sealed class DiscordClientSettings : ISetting
{
    public required string Token { get; init; }
    public required IReadOnlyList<GatewayIntents> Intents { get; init; } = Array.Empty<GatewayIntents>();

    public static string SectionName => nameof(DiscordClientSettings);
}