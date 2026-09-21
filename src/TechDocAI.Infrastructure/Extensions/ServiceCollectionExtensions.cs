using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using Qdrant.Client;
using System.Reflection;
using TechDocAI.Core.Common;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Persistence;
using TechDocAI.Infrastructure.Storage;
using TechDocAI.Infrastructure.Workers;

namespace TechDocAI.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHttpClient();

        // 1. PostgreSQL DbContext (when not testing)
        if (!environment.IsEnvironment("Testing"))
        {
            var connectionString = configuration.GetConnectionString("Postgres")
                                   ?? "Host=localhost;Port=5432;Database=techdoc;Username=techdoc;Password=techdoc";

            services.AddDbContext<TechDocDbContext>(options =>
                options.UseNpgsql(connectionString));

            var qdrantConfig = configuration.GetSection("Qdrant");
            var qdrantEndpoint = qdrantConfig["Endpoint"] ?? "http://localhost:6333";
            services.AddSingleton(new QdrantClient(new Uri(qdrantEndpoint)));
        }

        // 2. MinIO Client & Document Storage
        var storageConfig = configuration.GetSection("ObjectStorage");
        var endpoint = storageConfig["Endpoint"] ?? "http://localhost:9000";
        var accessKey = storageConfig["AccessKey"] ?? "techdoc";
        var secretKey = storageConfig["SecretKey"] ?? "techdocdev";
        var bucketName = storageConfig["Bucket"] ?? "documents";

        var minioUri = new Uri(endpoint);
        services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint(minioUri.Host, minioUri.Port)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(minioUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                .Build());

        services.AddSingleton<IDocumentStorage>(sp =>
            new MinIoDocumentStorage(sp.GetRequiredService<IMinioClient>(), bucketName));

        // 3. Hosted Service
        services.AddHostedService<IngestionJobBackgroundWorker>();

        // 4. Auto-register convention dependencies from Infrastructure and Core assemblies
        services.AddConventionDependencies(
            typeof(TechDocDbContext).Assembly,
            typeof(IScopedDependency).Assembly);

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
