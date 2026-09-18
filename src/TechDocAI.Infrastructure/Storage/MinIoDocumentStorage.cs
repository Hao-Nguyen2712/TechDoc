using Minio;
using Minio.DataModel.Args;
using TechDocAI.Core.Interfaces;

namespace TechDocAI.Infrastructure.Storage;

public class MinIoDocumentStorage : IDocumentStorage
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;

    public MinIoDocumentStorage(IMinioClient minioClient, string bucketName = "documents")
    {
        _minioClient = minioClient;
        _bucketName = bucketName;
    }

    public async Task SaveAsync(Guid documentId, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName), ct);
        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName), ct);
        }

        var objectName = $"documents/{documentId}/original.pdf";
        fileStream.Position = 0;

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType), ct);
    }

    public async Task<Stream> OpenReadAsync(Guid documentId, CancellationToken ct = default)
    {
        var objectName = $"documents/{documentId}/original.pdf";
        var memoryStream = new MemoryStream();

        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream)), ct);

        memoryStream.Position = 0;
        return memoryStream;
    }
}
