using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using Qdrant.Client;
using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Extensions;
using TechDocAI.Infrastructure.Persistence;
using TechDocAI.Infrastructure.Storage;

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

        // 4. Auto-register convention dependencies from the Infrastructure assembly
        services.AddConventionDependencies(typeof(TechDocDbContext).Assembly);

        return services;
    }
}
