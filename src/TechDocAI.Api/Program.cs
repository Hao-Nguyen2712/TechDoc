using Microsoft.EntityFrameworkCore;
using Minio;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Chunkers;
using TechDocAI.Infrastructure.Extractors;
using TechDocAI.Infrastructure.Persistence;
using TechDocAI.Infrastructure.Queue;
using TechDocAI.Infrastructure.Services;
using TechDocAI.Infrastructure.Storage;
using TechDocAI.Infrastructure.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Postgres DbContext (only if not Testing)
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
                           ?? "Host=localhost;Port=5432;Database=techdoc;Username=techdoc;Password=techdoc";

    builder.Services.AddDbContext<TechDocDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// MinIO Client & Document Storage
var storageConfig = builder.Configuration.GetSection("ObjectStorage");
var endpoint = storageConfig["Endpoint"] ?? "http://localhost:9000";
var accessKey = storageConfig["AccessKey"] ?? "techdoc";
var secretKey = storageConfig["SecretKey"] ?? "techdocdev";
var bucketName = storageConfig["Bucket"] ?? "documents";

var minioUri = new Uri(endpoint);
builder.Services.AddSingleton<IMinioClient>(_ =>
    new MinioClient()
        .WithEndpoint(minioUri.Host, minioUri.Port)
        .WithCredentials(accessKey, secretKey)
        .WithSSL(minioUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        .Build());

builder.Services.AddSingleton<IDocumentStorage>(sp =>
    new MinIoDocumentStorage(sp.GetRequiredService<IMinioClient>(), bucketName));

// Ingestion Pipeline Services & Background Worker
builder.Services.AddSingleton<IIngestionJobQueue, ChannelIngestionJobQueue>();
builder.Services.AddTransient<IDocumentExtractor, PdfPigExtractor>();
builder.Services.AddTransient<IDocumentChunker, NaiveChunker>();
builder.Services.AddTransient<IngestionPipelineProcessor>();

builder.Services.AddHostedService<IngestionJobBackgroundWorker>();

var app = builder.Build();

// Ensure Database Created only if not testing
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
        dbContext.Database.EnsureCreated();
    }
    catch (Exception)
    {
        // Ignore on startup if backing DB is not yet available
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
