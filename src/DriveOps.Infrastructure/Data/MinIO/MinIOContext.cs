using Minio;
using Minio.DataModel.Args;

namespace DriveOps.Infrastructure.Data.MinIO;

public class MinIOContext
{
    private readonly IMinioClient _minioClient;
    private readonly string _defaultBucket;

    public MinIOContext(IMinioClient minioClient, string defaultBucket = "driveops-files")
    {
        _minioClient = minioClient;
        _defaultBucket = defaultBucket;
    }

    public IMinioClient Client => _minioClient;
    public string DefaultBucket => _defaultBucket;

    public async Task EnsureBucketExistsAsync(string? bucketName = null, CancellationToken cancellationToken = default)
    {
        var bucket = bucketName ?? _defaultBucket;
        
        var bucketExistsArgs = new BucketExistsArgs()
            .WithBucket(bucket);
            
        bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);
        
        if (!found)
        {
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(bucket);
                
            await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
        }
    }
}