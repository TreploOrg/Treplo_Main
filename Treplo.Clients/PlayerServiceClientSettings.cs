using System.Text.Json;
using Treplo.Infrastructure.Configuration;

namespace Treplo.Clients;

public sealed class PlayerServiceClientSettings : ISetting
{
    public required string PlaybackEndpointUrl { get; init; }
    public JsonSerializerDefaults? JsonSerializerPreset { get; init;  }

    public static string SectionName => nameof(PlayerServiceClientSettings);
}