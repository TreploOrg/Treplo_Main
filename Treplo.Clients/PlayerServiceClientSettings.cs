using System.Text.Json;

namespace Treplo.Clients;

public sealed class PlayerServiceClientSettings
{
    public required string PlaybackEndpointUrl { get; init; }
    public JsonSerializerDefaults? JsonSerializerPreset { get; init;  }
}