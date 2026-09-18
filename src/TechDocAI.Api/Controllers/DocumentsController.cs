using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using TechDocAI.Core.Entities;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Api.Controllers;

[ApiController]
[Route("documents")]
public class DocumentsController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    private readonly IDocumentStorage _storage;
    private readonly IIngestionJobQueue _queue;
    private readonly TechDocDbContext _dbContext;

    public DocumentsController(
        IDocumentStorage storage,
        IIngestionJobQueue queue,
        TechDocDbContext dbContext)
    {
        _storage = storage;
        _queue = queue;
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { error = "File size exceeds maximum allowed limit of 50MB." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".pdf")
        {
            return BadRequest(new { error = "Only PDF files are supported." });
        }

        await using var stream = file.OpenReadStream();
        var buffer = new byte[PdfMagicBytes.Length];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));

        if (bytesRead < PdfMagicBytes.Length || !buffer.SequenceEqual(PdfMagicBytes))
        {
            return BadRequest(new { error = "Invalid PDF file header." });
        }

        // Reset stream position and compute SHA256 hash
        stream.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(stream);
        var contentHash = Convert.ToHexStringLower(hashBytes);

        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var storageKey = $"documents/{documentId}/original.pdf";

        // Save binary to Storage
        stream.Position = 0;
        await _storage.SaveAsync(documentId, stream, file.ContentType ?? "application/pdf", HttpContext.RequestAborted);

        // Save Document & IngestionJob to Database
        var document = new Document
        {
            Id = documentId,
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/pdf",
            FileSizeBytes = file.Length,
            ContentHash = contentHash,
            StorageKey = storageKey,
            NeedsOcr = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var job = new IngestionJob
        {
            Id = jobId,
            DocumentId = documentId,
            Status = IngestionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Documents.Add(document);
        _dbContext.IngestionJobs.Add(job);
        await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        // Enqueue background processing job
        await _queue.EnqueueAsync(jobId, HttpContext.RequestAborted);

        return Accepted(new { documentId, jobId });
    }
}
