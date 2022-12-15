using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Treplo.Common.Models;

namespace Treplo.Clients;

public sealed class PlayerServiceClient
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string requestUri;

    public PlayerServiceClient(IHttpClientFactory httpClientFactory, IOptions<PlayerServiceClientSettings> options)
    {
        this.httpClientFactory = httpClientFactory;
        requestUri = $"{options.Value.ServiceUrl}";
    }

    public async Task<Stream> PlayAsync(
        ulong sessionId,
        CancellationToken cancellationToken = default
    )
    {
        using var httpClient = httpClientFactory.CreateClient();
        using var content = JsonContent.Create(new { sessionId });
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{requestUri}/play")
        {
            Content = content,
        };
        var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<Guid> StartSearchAsync(
        ulong sessionId,
        TrackRequest[] searchTracks,
        CancellationToken cancellationToken = default
    )
    {
        using var httpClient = httpClientFactory.CreateClient();
        var result = await httpClient.PostAsJsonAsync($"{requestUri}/search-start",
            new { sessionId, searchTracks }, cancellationToken
        );
        result.EnsureSuccessStatusCode();

        var guid = await result.Content.ReadAsStringAsync(cancellationToken);
        return Guid.Parse(guid.AsSpan()[1..^1]);
    }

    public async Task RespondToSearchAsync(
        ulong sessionId,
        Guid searchSessionId,
        uint searchResultIndex,
        CancellationToken cancellationToken = default
    )
    {
        using var httpClient = httpClientFactory.CreateClient();
        var result = await httpClient.PostAsJsonAsync($"{requestUri}/search-respond",
            new { sessionId, searchSessionId, searchResultIndex }, cancellationToken
        );

        result.EnsureSuccessStatusCode();
    }

    public async Task EnqueueAsync(ulong sessionId, TrackRequest track, CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient();
        var result = await httpClient.PostAsJsonAsync($"{requestUri}/enqueue", new { sessionId, track },
            cancellationToken);

        result.EnsureSuccessStatusCode();
    }
}

public class PlayerServiceClientSettings
{
    public required string ServiceUrl { get; init; }
}