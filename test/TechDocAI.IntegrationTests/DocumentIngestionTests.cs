using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TechDocAI.Infrastructure.Persistence;
using Xunit;

namespace TechDocAI.IntegrationTests;

public class DocumentIngestionTests : IClassFixture<TechDocWebApplicationFactory>
{
    private readonly TechDocWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentIngestionTests(TechDocWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record UploadResponse(Guid documentId, Guid jobId);
    private record JobStatusResponse(Guid id, Guid documentId, string status, string? error, DateTimeOffset createdAt, DateTimeOffset updatedAt);

    [Fact]
    public async Task UploadValidPdf_ProcessesToDone_PersistsDocumentAndChunks()
    {
        // Arrange: create a PDF with valid text content
        var sampleText = "TechDoc is a retrieval-augmented QA assistant for technical documentation. " +
                         "Users upload documents in PDF or TXT formats and receive answers with exact page citations.";
        var pdfBytes = PdfTestHelper.CreatePdfWithText(sampleText);

        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        formContent.Add(fileContent, "file", "techdoc-guide.pdf");

        // Act 1: POST /documents
        var uploadResponse = await _client.PostAsync("/documents", formContent);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        Assert.True(uploadResponse.StatusCode == HttpStatusCode.Accepted, $"Upload failed with: {uploadBody}");

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        Assert.NotEqual(Guid.Empty, uploadResult.documentId);
        Assert.NotEqual(Guid.Empty, uploadResult.jobId);

        // Verify storage received the file
        Assert.True(InMemoryDocumentStorage.Files.ContainsKey(uploadResult.documentId));

        // Act 2: Poll GET /ingestion-jobs/{jobId} until done or failed
        JobStatusResponse? jobStatus = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var statusResponse = await _client.GetAsync($"/ingestion-jobs/{uploadResult.jobId}");
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

            jobStatus = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponse>();
            if (jobStatus != null && (jobStatus.status == "done" || jobStatus.status == "failed"))
            {
                break;
            }
        }

        // Assert
        Assert.NotNull(jobStatus);
        Assert.Equal("done", jobStatus.status);
        Assert.Null(jobStatus.error);

        // Verify database records
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
        var doc = await db.Documents.Include(d => d.Chunks).FirstOrDefaultAsync(d => d.Id == uploadResult.documentId);

        Assert.NotNull(doc);
        Assert.False(string.IsNullOrWhiteSpace(doc.ContentHash));
        Assert.NotEmpty(doc.Chunks);

        foreach (var chunk in doc.Chunks)
        {
            Assert.Equal("gemini-embedding-001", chunk.EmbeddingModel);
            Assert.Equal(1536, chunk.EmbeddingDimensions);

            // Verify point was upserted into VectorStore with ADR 0005 payload
            Assert.True(_factory.VectorStore.Points.TryGetValue(chunk.Id, out var vectorPoint));
            Assert.NotNull(vectorPoint);
            Assert.Equal(uploadResult.documentId, vectorPoint.DocumentId);
            Assert.Equal(chunk.PageIndex, vectorPoint.PageIndex);
            Assert.Equal(chunk.HeadingPath, vectorPoint.HeadingPath);
            Assert.Equal(chunk.Text, vectorPoint.Text);
            Assert.Equal("gemini-embedding-001", vectorPoint.EmbeddingModel);
            Assert.Equal(1536, vectorPoint.DenseVector.Length);
            Assert.NotEmpty(vectorPoint.SparseIndices);
            Assert.Equal(vectorPoint.SparseIndices.Length, vectorPoint.SparseValues.Length);
        }
    }

    [Fact]
    public async Task UploadDocument_WhenEmbeddingFails_RollsBackAllQdrantPointsAndPostgresChunks()
    {
        // Arrange
        _factory.EmbeddingGenerator.ShouldFail = true;
        try
        {
            var pdfBytes = PdfTestHelper.CreatePdfWithText("Rollback test text for embedding failure verification.");

            using var formContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(pdfBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            formContent.Add(fileContent, "file", "rollback-test.pdf");

            // Act 1: POST /documents
            var uploadResponse = await _client.PostAsync("/documents", formContent);
            Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);

            var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
            Assert.NotNull(uploadResult);

            // Act 2: Poll GET /ingestion-jobs/{jobId} until terminal status
            JobStatusResponse? jobStatus = null;
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(200);
                var statusResponse = await _client.GetAsync($"/ingestion-jobs/{uploadResult.jobId}");
                jobStatus = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponse>();

                if (jobStatus != null && (jobStatus.status == "done" || jobStatus.status == "failed"))
                {
                    break;
                }
            }

            // Assert: Job status is failed with error message
            Assert.NotNull(jobStatus);
            Assert.Equal("failed", jobStatus.status);
            Assert.Contains("Simulated embedding generation failure", jobStatus.error);

            // Assert: 0 chunks in PostgreSQL
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
            var chunks = await db.Chunks.Where(c => c.DocumentId == uploadResult.documentId).ToListAsync();
            Assert.Empty(chunks);

            // Assert: 0 points in VectorStore for this document
            var pointsForDoc = _factory.VectorStore.Points.Values
                .Where(p => p.DocumentId == uploadResult.documentId)
                .ToList();
            Assert.Empty(pointsForDoc);
        }
        finally
        {
            _factory.EmbeddingGenerator.ShouldFail = false;
        }
    }

    [Fact]
    public async Task UploadEmptyPdf_ProcessesToFailed_WithClearErrorMessage()
    {
        // Arrange: create an empty PDF with no extractable text
        var pdfBytes = PdfTestHelper.CreateEmptyPdf();

        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        formContent.Add(fileContent, "file", "empty-scan.pdf");

        // Act 1: POST /documents
        var uploadResponse = await _client.PostAsync("/documents", formContent);
        var body = await uploadResponse.Content.ReadAsStringAsync();
        Assert.True(uploadResponse.StatusCode == HttpStatusCode.Accepted, $"Failed with: {body}");

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);

        // Act 2: Poll GET /ingestion-jobs/{jobId} until terminal status
        JobStatusResponse? jobStatus = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var statusResponse = await _client.GetAsync($"/ingestion-jobs/{uploadResult.jobId}");
            jobStatus = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponse>();

            if (jobStatus != null && (jobStatus.status == "done" || jobStatus.status == "failed"))
            {
                break;
            }
        }

        // Assert
        Assert.NotNull(jobStatus);
        Assert.Equal("failed", jobStatus.status);
        Assert.Contains("no extractable text", jobStatus.error, StringComparison.OrdinalIgnoreCase);

        // Verify Document was flagged needsOcr
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == uploadResult.documentId);
        Assert.NotNull(doc);
        Assert.True(doc.NeedsOcr);
    }

    [Fact]
    public async Task GetIngestionJob_WithNonExistentId_Returns404NotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _client.GetAsync($"/ingestion-jobs/{nonExistentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadValidTxt_ProcessesToDone_PersistsDocumentAndChunksWithLineNumbers()
    {
        // Arrange: plain text document
        var textContent = "Line 1: System requirements and prerequisites.\n" +
                          "Line 2: Install .NET 10 SDK and Docker runtime.\n" +
                          "Line 3: Clone repository and configure settings.\n" +
                          "Line 4: Run dotnet run to start the application.";

        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(textContent));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        formContent.Add(fileContent, "file", "install-instructions.txt");

        // Act 1: POST /documents
        var uploadResponse = await _client.PostAsync("/documents", formContent);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        Assert.True(uploadResponse.StatusCode == HttpStatusCode.Accepted, $"Upload failed with: {uploadBody}");

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);

        // Act 2: Poll GET /ingestion-jobs/{jobId} until done
        JobStatusResponse? jobStatus = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var statusResponse = await _client.GetAsync($"/ingestion-jobs/{uploadResult.jobId}");
            jobStatus = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponse>();
            if (jobStatus != null && (jobStatus.status == "done" || jobStatus.status == "failed"))
            {
                break;
            }
        }

        // Assert
        Assert.NotNull(jobStatus);
        Assert.Equal("done", jobStatus.status);

        // Verify Chunks in DB have line numbers, null pageIndex, empty headingPath
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
        var doc = await db.Documents.Include(d => d.Chunks).FirstOrDefaultAsync(d => d.Id == uploadResult.documentId);

        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Chunks);
        foreach (var chunk in doc.Chunks)
        {
            Assert.Null(chunk.PageIndex);
            Assert.NotNull(chunk.StartLine);
            Assert.NotNull(chunk.EndLine);
            Assert.True(chunk.StartLine <= chunk.EndLine);
            Assert.Equal(string.Empty, chunk.HeadingPath);
        }
    }

    [Fact]
    public async Task UploadPdfWithHeadings_ProcessesToDone_PersistsChunksWithHeadingPaths()
    {
        // Arrange: PDF with 2 headings and body paragraphs
        var pdfBytes = PdfTestHelper.CreatePdfWithHeadings(
            "Overview",
            "TechDoc provides high-accuracy documentation answers with exact citations.",
            "Architecture",
            "The architecture consists of clean horizontal layers and deep vertical slices.");

        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        formContent.Add(fileContent, "file", "architecture.pdf");

        // Act 1: POST /documents
        var uploadResponse = await _client.PostAsync("/documents", formContent);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);

        // Act 2: Poll GET /ingestion-jobs/{jobId} until done
        JobStatusResponse? jobStatus = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var statusResponse = await _client.GetAsync($"/ingestion-jobs/{uploadResult.jobId}");
            jobStatus = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponse>();
            if (jobStatus != null && (jobStatus.status == "done" || jobStatus.status == "failed"))
            {
                break;
            }
        }

        // Assert
        Assert.NotNull(jobStatus);
        Assert.Equal("done", jobStatus.status);

        // Verify Chunks in DB have HeadingPath set
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
        var doc = await db.Documents.Include(d => d.Chunks).FirstOrDefaultAsync(d => d.Id == uploadResult.documentId);

        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Chunks);
        var headingPaths = doc.Chunks.Select(c => c.HeadingPath).ToList();
        Assert.Contains(headingPaths, p => p.Contains("Overview") || p.Contains("Architecture"));
    }
}
