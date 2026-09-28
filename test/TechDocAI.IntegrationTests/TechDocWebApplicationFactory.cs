using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Data.Common;
using TechDocAI.Application.Abstractions;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.IntegrationTests;

public class InMemoryDocumentStorage : IDocumentStorage
{
    public static ConcurrentDictionary<string, byte[]> Files { get; } = new();

    public async Task SaveAsync(string storageKey, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        Files[storageKey] = ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        if (Files.TryGetValue(storageKey, out var bytes))
        {
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        throw new FileNotFoundException($"Document {storageKey} not found in storage.");
    }
}

public class SqliteBusyRetryHandler : DelegatingHandler
{
    // The factory shares one in-memory SQLite connection between the hosted worker's
    // write transactions and request scopes; SQLITE_BUSY can surface as an in-process
    // exception on any request. Retrying is safe: uploads are idempotent (ADR 0009),
    // reads are side-effect free, and a busy hit means nothing committed yet.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < 30)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is SqliteException
        || (ex is InvalidOperationException && ex.Message.Contains("SQLite", StringComparison.OrdinalIgnoreCase));
}

public class TechDocWebApplicationFactory : WebApplicationFactory<Program>
{
    // Anchor connection keeping the shared in-memory database alive. Each EF context
    // opens its OWN connection to the same named in-memory database: sharing one
    // SqliteConnection object across concurrently-used contexts (worker + requests)
    // fails with SQLITE_BUSY during connection initialization.
    private DbConnection? _anchorConnection;
    private string? _dataSource;

    public InMemoryDocumentStorage DocumentStorage { get; } = new();
    public InMemoryVectorStore VectorStore { get; } = new();
    public StubEmbeddingGenerator EmbeddingGenerator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            _dataSource = $"file:{Guid.NewGuid():N}?mode=memory&cache=shared";
            _anchorConnection = new SqliteConnection($"Data Source={_dataSource};Default Timeout=5;");
            _anchorConnection.Open();

            services.AddDbContext<TechDocDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_dataSource};Default Timeout=5;");
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
        _anchorConnection?.Dispose();
    }
}
