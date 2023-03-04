using Microsoft.Extensions.DependencyInjection;

namespace Treplo.Common.OrleansGrpcConnector;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConverters(this IServiceCollection serviceCollection)
    {
        var types = typeof(ServiceCollectionExtensions).Assembly
            .GetTypes()
            .Select(
                type => (
                    type,
                    interfaces: type.FindInterfaces(
                        (i, _) => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConverter<,>),
                        null
                    )
                )
            ).SelectMany(pair => pair.interfaces.Select(x => (type: pair.type, @interface: x)));

        foreach (var (t, i) in types)
        {
            serviceCollection.Add(new ServiceDescriptor(i, t, ServiceLifetime.Singleton));
        }

        return serviceCollection;
    }
}