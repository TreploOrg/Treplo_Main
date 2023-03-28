using System.Net;
using System.Reflection;
using MediatR;
using Treplo.SearchService.Searching;
using Treplo.SearchService.Searching.Youtube;

namespace Treplo.SearchService.Helpers;

public static class StartupHelpers
{
    public static IServiceCollection AddYoutubeEngine(this IServiceCollection services)
    {
        services.AddHttpClient<YoutubeEngine>().ConfigureHttpMessageHandlerBuilder(x =>
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false,
            };

            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            x.PrimaryHandler = handler;
        });

        services.AddSingleton<ISearchEngine, YoutubeEngine>();
        return services;
    }

    public static IServiceCollection AddSearchEngineManager(this IServiceCollection services)
    {
        services.AddSingleton<ISearchEngineManager, MixedSearchEngineManager>();
        return services;
    }
}