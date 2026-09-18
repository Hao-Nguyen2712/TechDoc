using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace TechDocAI.IntegrationTests;

public class DocumentValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DocumentValidationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostDocuments_WithNonPdfFile_Returns400BadRequest()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        var byteArrayContent = new ByteArrayContent("Hello world plain text"u8.ToArray());
        byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        content.Add(byteArrayContent, "file", "test.txt");

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
