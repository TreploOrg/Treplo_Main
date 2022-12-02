using Discord;

namespace Treplo;

public sealed class DiscordClientSettings
{
    public required string Token { get; init; }
    public required IReadOnlyList<GatewayIntents> Intents { get; init; } = Array.Empty<GatewayIntents>();
}