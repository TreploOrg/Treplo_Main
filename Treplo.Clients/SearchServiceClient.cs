using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Treplo.Common.Models;

namespace Treplo.Clients;

public class SearchServiceClient
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    private readonly string requestUri;

    public SearchServiceClient(HttpClient httpClient, IOptions<SearchServiceClientSettings> options)
    {
        this.httpClient = httpClient;
        requestUri = $"{options.Value.ServiceUrl}/search";
        var preset = options.Value.JsonSerializerPreset;
        if (preset is { } actualPreset)
            jsonSerializerOptions = new JsonSerializerOptions(actualPreset);
        else
            jsonSerializerOptions = JsonSerializerOptions.Default;
    }

    public async IAsyncEnumerable<TrackSearchResult> SearchAsync(
        string query,
        uint? limit = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var request = $"{requestUri}?query={query}";
        if (limit is { } l)
            request += $"&limit={l}";
        await using var resultStream = await httpClient.GetStreamAsync(request, cancellationToken);
        await foreach (var track in JsonSerializer.DeserializeAsyncEnumerable<TrackSearchResult>(resultStream,
            jsonSerializerOptions, cancellationToken))
        {
            yield return track;
        }
    }
}