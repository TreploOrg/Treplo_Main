using Discord;

namespace Treplo;

public sealed class DiscordClientSettings
{
    public string Token { get; set; }
    public IReadOnlyList<GatewayIntents> Intents { get; set; }
}