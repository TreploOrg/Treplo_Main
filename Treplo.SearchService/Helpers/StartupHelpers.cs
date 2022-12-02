using System.Net;
using System.Reflection;
using MediatR;
using Treplo.SearchService.Searching;
using Treplo.SearchService.Searching.Youtube;

namespace Treplo.SearchService.Helpers;

public static class StartupHelpers
{
    public static WebApplicationBuilder AddMediatr(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
        return builder;
    }

    public static WebApplicationBuilder AddYoutubeEngine(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<YoutubeEngine>().ConfigureHttpMessageHandlerBuilder(x =>
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false,
            };

            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            x.PrimaryHandler = handler;
        });

        builder.Services.AddSingleton<ISearchEngine, YoutubeEngine>();
        return builder;
    }

    public static WebApplicationBuilder AddSearchEngineManager(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ISearchEngineManager, MixedSearchEngineManager>();
        return builder;
    }
}