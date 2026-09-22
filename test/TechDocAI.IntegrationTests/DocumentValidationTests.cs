using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace TechDocAI.IntegrationTests;

public class DocumentValidationTests : IClassFixture<TechDocWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentValidationTests(TechDocWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostDocuments_WithUnsupportedFileExtension_Returns400BadRequest()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        var byteArrayContent = new ByteArrayContent("Binary document content"u8.ToArray());
        byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(byteArrayContent, "file", "document.docx");

        // Act
        var response = await _client.PostAsync("/documents", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDocuments_WithPdfExtensionButInvalidMagicBytes_Returns400BadRequest()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        var byteArrayContent = new ByteArrayContent("Not a real PDF header"u8.ToArray());
        byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(byteArrayContent, "file", "fake.pdf");

        // Act
        var response = await _client.PostAsync("/documents", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
