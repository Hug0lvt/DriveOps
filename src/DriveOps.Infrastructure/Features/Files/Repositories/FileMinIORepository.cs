using Minio;
using Minio.DataModel.Args;
using System.Security.Cryptography;
using DriveOps.Shared.Models.Files;
using DriveOps.Infrastructure.Data.MinIO;

namespace DriveOps.Infrastructure.Features.Files.Repositories;

public class FileMinIORepository
{
    private readonly MinIOContext _context;

    public FileMinIORepository(MinIOContext context)
    {
        _context = context;
    }

    public async Task<FileDocument> UploadFileAsync(
        Stream fileStream, 
        string fileName, 
        string contentType, 
        string uploadedBy,
        string? bucketName = null,
        Dictionary<string, string>? metadata = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var bucket = bucketName ?? _context.DefaultBucket;
        var objectKey = GenerateObjectKey(fileName);
        var fileSize = fileStream.Length;
        
        // S'assurer que le bucket existe
        await _context.EnsureBucketExistsAsync(bucket, cancellationToken);
        
        // Calculer le checksum
        var checksum = await CalculateChecksumAsync(fileStream);
        fileStream.Position = 0; // Reset stream position
        
        // Préparer les métadonnées MinIO
        var minioMetadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>())
        {
            ["uploaded-by"] = uploadedBy,
            ["original-filename"] = fileName,
            ["checksum"] = checksum,
            ["upload-date"] = DateTime.UtcNow.ToString("O")
        };

        // Upload vers MinIO
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(fileStream)
            .WithObjectSize(fileSize)
            .WithContentType(contentType)
            .WithHeaders(minioMetadata);

        await _context.Client.PutObjectAsync(putObjectArgs, cancellationToken);

        // Créer le document de métadonnées
        var fileDocument = new FileDocument
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileNameWithoutExtension(fileName),
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            BucketName = bucket,
            ObjectKey = objectKey,
            Checksum = checksum,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy,
            Tags = tags ?? new List<string>(),
            Metadata = metadata ?? new Dictionary<string, string>()
        };

        return fileDocument;
    }

    public async Task<Stream> DownloadFileAsync(
        string bucketName, 
        string objectKey, 
        CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        
        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream));

        await _context.Client.GetObjectAsync(getObjectArgs, cancellationToken);
        
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<Stream> DownloadFileAsync(
        FileDocument fileDocument, 
        CancellationToken cancellationToken = default)
    {
        return await DownloadFileAsync(fileDocument.BucketName, fileDocument.ObjectKey, cancellationToken);
    }

    public async Task<bool> DeleteFileAsync(
        string bucketName, 
        string objectKey, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey);

            await _context.Client.RemoveObjectAsync(removeObjectArgs, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteFileAsync(
        FileDocument fileDocument, 
        CancellationToken cancellationToken = default)
    {
        return await DeleteFileAsync(fileDocument.BucketName, fileDocument.ObjectKey, cancellationToken);
    }

    public async Task<bool> FileExistsAsync(
        string bucketName, 
        string objectKey, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey);

            await _context.Client.StatObjectAsync(statObjectArgs, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetPresignedUrlAsync(
        string bucketName, 
        string objectKey, 
        int expiryInSeconds = 3600, 
        CancellationToken cancellationToken = default)
    {
        var presignedGetObjectArgs = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry(expiryInSeconds);

        return await _context.Client.PresignedGetObjectAsync(presignedGetObjectArgs);
    }

    private static string GenerateObjectKey(string fileName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var guid = Guid.NewGuid().ToString("N")[..8];
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        
        // Structure : année/mois/jour/nom-fichier_guid.extension
        return $"{timestamp}/{nameWithoutExtension}_{guid}{extension}";
    }

    private static async Task<string> CalculateChecksumAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var originalPosition = stream.Position;
        stream.Position = 0;
        
        var hashBytes = await sha256.ComputeHashAsync(stream);
        stream.Position = originalPosition;
        
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}