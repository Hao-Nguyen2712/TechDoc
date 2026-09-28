using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;

namespace TechDocAI.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddConventionDependencies(typeof(IScopedDependency).Assembly);

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new EmbeddingSettings(
                config["Embedding:ModelId"] ?? "gemini-embedding-001",
                int.TryParse(config["Embedding:Dimensions"], out var dimensions) ? dimensions : 1536);
        });

        return services;
    }

    public static IServiceCollection AddConventionDependencies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var markerTypes = new HashSet<Type>
        {
            typeof(IScopedDependency),
            typeof(ITransientDependency),
            typeof(ISingletonDependency)
        };

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);

            foreach (var type in types)
            {
                ServiceLifetime? lifetime = null;

                if (typeof(IScopedDependency).IsAssignableFrom(type))
                {
                    lifetime = ServiceLifetime.Scoped;
                }
                else if (typeof(ITransientDependency).IsAssignableFrom(type))
                {
                    lifetime = ServiceLifetime.Transient;
                }
                else if (typeof(ISingletonDependency).IsAssignableFrom(type))
                {
                    lifetime = ServiceLifetime.Singleton;
                }

                if (lifetime == null)
                {
                    continue;
                }

                // Register for implemented interfaces (excluding markers and system interfaces)
                var interfaces = type.GetInterfaces()
                    .Where(i => !markerTypes.Contains(i) && i != typeof(IDisposable) && i != typeof(IAsyncDisposable))
                    .ToList();

                foreach (var iface in interfaces)
                {
                    services.Add(new ServiceDescriptor(iface, type, lifetime.Value));
                }

                // Also register the concrete class itself
                services.Add(new ServiceDescriptor(type, type, lifetime.Value));
            }
        }

        return services;
    }
}
