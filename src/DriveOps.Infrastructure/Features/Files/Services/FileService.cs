using DriveOps.Shared.Models.Files;
using DriveOps.Infrastructure.Data.MongoDb;
using DriveOps.Infrastructure.Data.MinIO;

namespace DriveOps.Infrastructure.Features.Files.Services;

public class FileService
{
    private readonly MongoDbUnitOfWork _mongoUnitOfWork;
    private readonly MinIOUnitOfWork _minioUnitOfWork;

    public FileService(MongoDbUnitOfWork mongoUnitOfWork, MinIOUnitOfWork minioUnitOfWork)
    {
        _mongoUnitOfWork = mongoUnitOfWork;
        _minioUnitOfWork = minioUnitOfWork;
    }

    /// <summary>
    /// Upload un fichier : stockage physique dans MinIO + m�tadonn�es dans MongoDB
    /// </summary>
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
        // 1. Upload vers MinIO (stockage physique)
        var fileDocument = await _minioUnitOfWork.FileRepository.UploadFileAsync(
            fileStream, fileName, contentType, uploadedBy, bucketName, metadata, tags, cancellationToken);

        // 2. Sauvegarder les m�tadonn�es dans MongoDB  
        // Note: Il faudrait cr�er un FileDocumentRepository pour MongoDB
        // Pour l'instant on retourne juste le FileDocument avec les infos MinIO
        
        return fileDocument;
    }

    /// <summary>
    /// Download un fichier depuis MinIO
    /// </summary>
    public async Task<Stream> DownloadFileAsync(
        FileDocument fileDocument,
        CancellationToken cancellationToken = default)
    {
        return await _minioUnitOfWork.FileRepository.DownloadFileAsync(fileDocument, cancellationToken);
    }

    /// <summary>
    /// Supprime un fichier : MinIO + m�tadonn�es MongoDB
    /// </summary>
    public async Task<bool> DeleteFileAsync(
        FileDocument fileDocument,
        CancellationToken cancellationToken = default)
    {
        // 1. Supprimer le fichier physique de MinIO
        var deleted = await _minioUnitOfWork.FileRepository.DeleteFileAsync(fileDocument, cancellationToken);
        
        // 2. Marquer comme supprim� dans MongoDB
        // Note: Il faudrait impl�menter la suppression dans FileDocumentRepository
        
        return deleted;
    }

    /// <summary>
    /// G�n�re une URL pr�sign�e pour acc�s temporaire au fichier
    /// </summary>
    public async Task<string> GetPresignedDownloadUrlAsync(
        FileDocument fileDocument,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        return await _minioUnitOfWork.FileRepository.GetPresignedUrlAsync(
            fileDocument.BucketName, fileDocument.ObjectKey, expiryInSeconds, cancellationToken);
    }

    /// <summary>
    /// Exemple d'usage : upload d'un fichier li� � un Sample
    /// </summary>
    public async Task<FileDocument> UploadSampleFileAsync(
        Guid sampleId,
        Stream fileStream,
        string fileName,
        string contentType,
        string uploadedBy,
        CancellationToken cancellationToken = default)
    {
        var tags = new List<string> { "sample", $"sample-{sampleId}" };
        var metadata = new Dictionary<string, string>
        {
            ["sample-id"] = sampleId.ToString(),
            ["category"] = "sample-attachment"
        };

        return await UploadFileAsync(
            fileStream, fileName, contentType, uploadedBy, 
            bucketName: "sample-files", metadata, tags, cancellationToken);
    }
}