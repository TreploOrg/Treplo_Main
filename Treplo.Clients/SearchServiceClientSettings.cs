using System.Text.Json;

namespace Treplo.Clients;

public sealed class SearchServiceClientSettings
{
    public string ServiceUrl { get; set; }
    public JsonSerializerDefaults? JsonSerializerPreset { get; set; } 
}