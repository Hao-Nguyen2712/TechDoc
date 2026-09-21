using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Data.Common;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.IntegrationTests;

public class InMemoryDocumentStorage : IDocumentStorage
{
    public static ConcurrentDictionary<Guid, byte[]> Files { get; } = new();

    public async Task SaveAsync(Guid documentId, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        Files[documentId] = ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(Guid documentId, CancellationToken ct = default)
    {
        if (Files.TryGetValue(documentId, out var bytes))
        {
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        throw new FileNotFoundException($"Document {documentId} not found in storage.");
    }
}

public class TechDocWebApplicationFactory : WebApplicationFactory<Program>
{
    private DbConnection? _connection;

    public InMemoryDocumentStorage DocumentStorage { get; } = new();
    public InMemoryVectorStore VectorStore { get; } = new();
    public StubEmbeddingGenerator EmbeddingGenerator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<TechDocDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Replace IDocumentStorage with InMemoryDocumentStorage
            var storageDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDocumentStorage));
            if (storageDescriptor != null)
            {
                services.Remove(storageDescriptor);
            }
            services.AddSingleton<IDocumentStorage>(DocumentStorage);

            // Replace IVectorStore with InMemoryVectorStore
            var vectorStoreDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IVectorStore));
            if (vectorStoreDescriptor != null)
            {
                services.Remove(vectorStoreDescriptor);
            }
            services.AddSingleton<IVectorStore>(VectorStore);

            // Replace IEmbeddingGenerator with StubEmbeddingGenerator
            var embeddingDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
            if (embeddingDescriptor != null)
            {
                services.Remove(embeddingDescriptor);
            }
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(EmbeddingGenerator);

            // Create DB schema in SQLite
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}
