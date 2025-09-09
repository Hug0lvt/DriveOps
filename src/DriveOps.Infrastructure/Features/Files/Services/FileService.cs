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
        var savedDocument = await _mongoUnitOfWork.FileDocumentRepository.CreateAsync(fileDocument, cancellationToken);
        
        return savedDocument;
    }

    /// <summary>
    /// R�cup�re un fichier par son ID depuis MongoDB puis download depuis MinIO
    /// </summary>
    public async Task<(FileDocument? document, Stream? fileStream)> GetFileAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        // 1. R�cup�rer les m�tadonn�es depuis MongoDB
        var fileDocument = await _mongoUnitOfWork.FileDocumentRepository.GetByIdAsync(fileId, cancellationToken);
        if (fileDocument == null)
        {
            return (null, null);
        }

        // 2. Download le fichier depuis MinIO
        var fileStream = await _minioUnitOfWork.FileRepository.DownloadFileAsync(fileDocument, cancellationToken);
        
        return (fileDocument, fileStream);
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
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        // 1. R�cup�rer les m�tadonn�es
        var fileDocument = await _mongoUnitOfWork.FileDocumentRepository.GetByIdAsync(fileId, cancellationToken);
        if (fileDocument == null)
        {
            return false;
        }

        // 2. Supprimer le fichier physique de MinIO
        var deleted = await _minioUnitOfWork.FileRepository.DeleteFileAsync(fileDocument, cancellationToken);
        
        // 3. Marquer comme supprim� dans MongoDB
        if (deleted)
        {
            await _mongoUnitOfWork.FileDocumentRepository.DeleteAsync(fileId, cancellationToken);
        }
        
        return deleted;
    }

    /// <summary>
    /// Supprime un fichier par son document
    /// </summary>
    public async Task<bool> DeleteFileAsync(
        FileDocument fileDocument,
        CancellationToken cancellationToken = default)
    {
        return await DeleteFileAsync(fileDocument.Id, cancellationToken);
    }

    /// <summary>
    /// G�n�re une URL pr�sign�e pour acc�s temporaire au fichier
    /// </summary>
    public async Task<string> GetPresignedDownloadUrlAsync(
        Guid fileId,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        var fileDocument = await _mongoUnitOfWork.FileDocumentRepository.GetByIdAsync(fileId, cancellationToken);
        if (fileDocument == null)
        {
            throw new InvalidOperationException($"File {fileId} not found");
        }

        return await _minioUnitOfWork.FileRepository.GetPresignedUrlAsync(
            fileDocument.BucketName, fileDocument.ObjectKey, expiryInSeconds, cancellationToken);
    }

    /// <summary>
    /// Recherche de fichiers par tags
    /// </summary>
    public async Task<List<FileDocument>> SearchFilesByTagsAsync(
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        return await _mongoUnitOfWork.FileDocumentRepository.SearchByTagsAsync(tags, cancellationToken);
    }

    /// <summary>
    /// R�cup�re les fichiers d'un utilisateur
    /// </summary>
    public async Task<List<FileDocument>> GetUserFilesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _mongoUnitOfWork.FileDocumentRepository.GetByUploadedByAsync(userId, cancellationToken);
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

    /// <summary>
    /// R�cup�re tous les fichiers li�s � un Sample
    /// </summary>
    public async Task<List<FileDocument>> GetSampleFilesAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        return await _mongoUnitOfWork.FileDocumentRepository.GetSampleFilesAsync(sampleId, cancellationToken);
    }
}