using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Treplo.Infrastructure;

public static class ConfigurationExtensions
{
    public static IHostBuilder BindOption<T>(this IHostBuilder builder) where T : class
    {
        builder.ConfigureServices((ctx, services) =>
        {
            services.Configure<T>(
                ctx.Configuration
                    .GetSection(typeof(T).Name));
            services.AddOptions<T>();
        });

        return builder;
    }
}