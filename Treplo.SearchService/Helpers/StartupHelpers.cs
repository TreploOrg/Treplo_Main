using System.Net;
using Treplo.SearchService.Searching;
using Treplo.SearchService.Searching.Youtube;

namespace Treplo.SearchService.Helpers;

public static class StartupHelpers
{
    public static IServiceCollection AddYoutubeEngine(this IServiceCollection services)
    {
        services.AddHttpClient<YoutubeEngine>().ConfigureHttpMessageHandlerBuilder(
            x =>
            {
                var handler = new HttpClientHandler
                {
                    UseCookies = false,
                };

                if (handler.SupportsAutomaticDecompression)
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                handler.CookieContainer.Add(new Cookie("CONSENT", "YES+cb", "/", ".youtube.com"));
                x.PrimaryHandler = handler;
            }
        ).ConfigureHttpClient(
            x => x.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.111 Safari/537.36"
            )
        );

        services.AddSingleton<ISearchEngine, YoutubeEngine>();
        return services;
    }

    public static IServiceCollection AddSearchEngineManager(this IServiceCollection services)
    {
        services.AddSingleton<ISearchEngineManager, MixedSearchEngineManager>();
        return services;
    }
}