namespace Treplo.SearchService.Tests;

public class HttpFactory: IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}