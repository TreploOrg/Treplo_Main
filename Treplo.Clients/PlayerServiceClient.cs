using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Treplo.Common.Models;

namespace Treplo.Clients;

public sealed class PlayerServiceClient
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string url;
    private readonly JsonSerializerOptions jsonSerializerOptions;

    public PlayerServiceClient(IOptions<PlayerServiceClientSettings> setting, IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
        url = setting.Value.PlaybackEndpointUrl + "/play";
        var preset = setting.Value.JsonSerializerPreset;
        if (preset is { } actualPreset)
            jsonSerializerOptions = new JsonSerializerOptions(actualPreset);
        else
            jsonSerializerOptions = JsonSerializerOptions.Default;
    }

    public async Task<Stream> GetAudioStream(StreamInfo streamInfo, StreamFormatRequest streamFormatRequest, CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient(nameof(PlayerServiceClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                StreamInfo = streamInfo, 
                FormatRequest = streamFormatRequest,
            }, options: jsonSerializerOptions),
        };

        //TODO: research whether or not response is disposed with its content stream and, if it's not, check if it is ok to let finalizers do their work or to make a stream wrapper
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}