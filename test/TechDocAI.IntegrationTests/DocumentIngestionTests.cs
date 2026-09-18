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
}
