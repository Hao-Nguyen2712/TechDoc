using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TechDocAI.Core.DTOs;
using TechDocAI.Infrastructure.Persistence;
using Xunit;

namespace TechDocAI.IntegrationTests;

public class DocumentDedupAndReadTests : IClassFixture<TechDocWebApplicationFactory>
{
    private readonly TechDocWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentDedupAndReadTests(TechDocWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record UploadResponse(Guid documentId, Guid jobId);
    private record JobStatusResponse(Guid id, Guid documentId, string status, string? error, DateTimeOffset createdAt, DateTimeOffset updatedAt);

    private async Task WaitForJobTerminalStatusAsync(Guid jobId)
    {
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var statusResponse = await _client.GetAsync($"/ingestion-jobs/{jobId}");
            if (statusResponse.IsSuccessStatusCode)
            {
                var jobStatus = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponse>();
                if (jobStatus != null && (jobStatus.status == "done" || jobStatus.status == "failed"))
                {
                    return;
                }
            }
        }
    }

    [Fact]
    public async Task GetDocuments_ReturnsOkWithListOfDocuments()
    {
        // Arrange: Upload a document first
        var textContent = "Sample documentation content for read endpoint test.";
        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(textContent));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        formContent.Add(fileContent, "file", "read-test.txt");

        var uploadResponse = await _client.PostAsync("/documents", formContent);
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        await WaitForJobTerminalStatusAsync(uploadResult.jobId);

        // Act
        var response = await _client.GetAsync("/documents");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var docs = await response.Content.ReadFromJsonAsync<List<DocumentResponse>>();
        Assert.NotNull(docs);
        Assert.Contains(docs, d => d.Id == uploadResult.documentId);
    }

    [Fact]
    public async Task GetDocumentById_WhenExists_ReturnsDocumentWithIngestionJobs()
    {
        // Arrange: Upload a document
        var textContent = "Detailed document content for get-by-id test.";
        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(textContent));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        formContent.Add(fileContent, "file", "details-test.txt");

        var uploadResponse = await _client.PostAsync("/documents", formContent);
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        await WaitForJobTerminalStatusAsync(uploadResult.jobId);

        // Act
        var response = await _client.GetAsync($"/documents/{uploadResult.documentId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var docDetails = await response.Content.ReadFromJsonAsync<DocumentDetailsResponse>();
        Assert.NotNull(docDetails);
        Assert.Equal(uploadResult.documentId, docDetails.Id);
        Assert.Equal("details-test.txt", docDetails.FileName);
        Assert.NotEmpty(docDetails.IngestionJobs);
        Assert.Contains(docDetails.IngestionJobs, j => j.Id == uploadResult.jobId);
    }

    [Fact]
    public async Task GetDocumentById_WhenNotFound_Returns404NotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _client.GetAsync($"/documents/{nonExistentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadDuplicateOfSuccessfulDocument_Returns200Ok_AndDoesNotCreateNewJobOrDoc()
    {
        // Arrange: Upload first document and wait until done
        var textContent = "TechDoc documentation content for idempotent duplicate upload test.";
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(textContent);

        using (var firstForm = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            firstForm.Add(fileContent, "file", "duplicate-test.txt");

            var firstUploadResponse = await _client.PostAsync("/documents", firstForm);
            Assert.Equal(HttpStatusCode.Accepted, firstUploadResponse.StatusCode);
            var firstResult = await firstUploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
            Assert.NotNull(firstResult);
            await WaitForJobTerminalStatusAsync(firstResult.jobId);

            // Act: Re-upload the exact same content
            using var secondForm = new MultipartFormDataContent();
            var secondFileContent = new ByteArrayContent(fileBytes);
            secondFileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            secondForm.Add(secondFileContent, "file", "duplicate-test.txt");

            var secondUploadResponse = await _client.PostAsync("/documents", secondForm);

            // Assert: Returns 200 OK with the existing Document
            Assert.Equal(HttpStatusCode.OK, secondUploadResponse.StatusCode);
            var doc = await secondUploadResponse.Content.ReadFromJsonAsync<DocumentDetailsResponse>();
            Assert.NotNull(doc);
            Assert.Equal(firstResult.documentId, doc.Id);

            // Assert DB: Only 1 Document and 1 IngestionJob exist for this content
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
            var docCount = await db.Documents.CountAsync(d => d.ContentHash == doc.ContentHash);
            Assert.Equal(1, docCount);

            var jobCount = await db.IngestionJobs.CountAsync(j => j.DocumentId == firstResult.documentId);
            Assert.Equal(1, jobCount);
        }
    }

    [Fact]
    public async Task UploadDuplicateOfFailedDocument_Returns202Accepted_AndEnqueuesNewJobForSameDocument()
    {
        // Arrange: Upload empty PDF that will fail ingestion
        var pdfBytes = PdfTestHelper.CreateEmptyPdf();

        using (var firstForm = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(pdfBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            firstForm.Add(fileContent, "file", "fail-and-recover.pdf");

            var firstUploadResponse = await _client.PostAsync("/documents", firstForm);
            Assert.Equal(HttpStatusCode.Accepted, firstUploadResponse.StatusCode);
            var firstResult = await firstUploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
            Assert.NotNull(firstResult);
            await WaitForJobTerminalStatusAsync(firstResult.jobId);

            // Verify first job failed
            using (var verifyScope = _factory.Services.CreateScope())
            {
                var dbVerify = verifyScope.ServiceProvider.GetRequiredService<TechDocDbContext>();
                var failedJob = await dbVerify.IngestionJobs.FirstOrDefaultAsync(j => j.Id == firstResult.jobId);
                Assert.NotNull(failedJob);
                Assert.Equal(Core.Entities.IngestionStatus.Failed, failedJob.Status);
            }

            // Act: Re-upload the exact same failed PDF
            using var secondForm = new MultipartFormDataContent();
            var secondFileContent = new ByteArrayContent(pdfBytes);
            secondFileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            secondForm.Add(secondFileContent, "file", "fail-and-recover.pdf");

            var secondUploadResponse = await _client.PostAsync("/documents", secondForm);

            // Assert: Returns 202 Accepted with same documentId, new jobId
            Assert.Equal(HttpStatusCode.Accepted, secondUploadResponse.StatusCode);
            var secondResult = await secondUploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
            Assert.NotNull(secondResult);
            Assert.Equal(firstResult.documentId, secondResult.documentId);
            Assert.NotEqual(firstResult.jobId, secondResult.jobId);

            // Assert DB: Still 1 Document row, but now 2 IngestionJob rows
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
            var docCount = await db.Documents.CountAsync(d => d.Id == firstResult.documentId);
            Assert.Equal(1, docCount);

            var jobCount = await db.IngestionJobs.CountAsync(j => j.DocumentId == firstResult.documentId);
            Assert.Equal(2, jobCount);
        }
    }
}
