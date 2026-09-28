using Minio;
using Minio.DataModel.Args;
using TechDocAI.Application.Abstractions;

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

    public async Task SaveAsync(string storageKey, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName), ct);
        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName), ct);
        }

        fileStream.Position = 0;

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(storageKey)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType), ct);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var memoryStream = new MemoryStream();

        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(storageKey)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream)), ct);

        memoryStream.Position = 0;
        return memoryStream;
    }
}
