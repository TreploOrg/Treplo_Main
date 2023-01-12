using System.Text.Json;
using Treplo.Infrastructure.Configuration;

namespace Treplo.Clients;

public sealed class SearchServiceClientSettings : ISetting
{
    public required string ServiceUrl { get; init; }
    public JsonSerializerDefaults? JsonSerializerPreset { get; init;  }

    public static string SectionName => nameof(SearchServiceClientSettings);
}