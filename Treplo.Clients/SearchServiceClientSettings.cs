using System.Text.Json;

namespace Treplo.Clients;

public sealed class SearchServiceClientSettings
{
    public required string ServiceUrl { get; init; }
    public JsonSerializerDefaults? JsonSerializerPreset { get; init; }
}