using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Treplo.Infrastructure.Configuration;

public static class ConfigurationExtensions
{
    public static IHostBuilder BindOption<T>(this IHostBuilder builder) where T : class, ISetting
    {
        builder.ConfigureServices(
            (ctx, services) =>
            {
                services.Configure<T>(
                    ctx.Configuration.GetSection(T.SectionName)
                );
            }
        );

        return builder;
    }

    public static T GetOptions<T>(this HostBuilderContext ctx) where T : class, ISetting
    {
        var section = ctx.Configuration.GetRequiredSection(T.SectionName);
        return section.Get<T>()!;
    }
}