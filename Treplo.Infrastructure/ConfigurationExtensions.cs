using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

    public static T GetOptions<T>(this HostBuilderContext ctx) where T : class
    {
        var section = ctx.Configuration.GetRequiredSection(typeof(T).Name);
        return section.Get<T>()!;
    }
}