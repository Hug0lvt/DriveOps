using MongoDB.Driver;
using DriveOps.Shared.Models.Files;
using DriveOps.Infrastructure.Data.MongoDb;

namespace DriveOps.Infrastructure.Features.Files.Repositories;

public class FileDocumentMongoRepository
{
    private readonly MongoDbContext _context;
    private readonly IMongoCollection<FileDocument> _collection;

    public FileDocumentMongoRepository(MongoDbContext context)
    {
        _context = context;
        _collection = context.FileDocuments;
    }

    public async Task<FileDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(doc => doc.Id == id && !doc.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<FileDocument?> GetByObjectKeyAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(doc => doc.BucketName == bucketName && doc.ObjectKey == objectKey && !doc.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<FileDocument>> GetByUploadedByAsync(string uploadedBy, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(doc => doc.UploadedBy == uploadedBy && !doc.IsDeleted)
            .SortByDescending(doc => doc.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FileDocument>> SearchByTagsAsync(List<string> tags, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FileDocument>.Filter.And(
            Builders<FileDocument>.Filter.AnyIn(doc => doc.Tags, tags),
            Builders<FileDocument>.Filter.Eq(doc => doc.IsDeleted, false)
        );

        return await _collection
            .Find(filter)
            .SortByDescending(doc => doc.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FileDocument>> GetByContentTypeAsync(string contentType, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(doc => doc.ContentType == contentType && !doc.IsDeleted)
            .SortByDescending(doc => doc.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<FileDocument> CreateAsync(FileDocument document, CancellationToken cancellationToken = default)
    {
        document.UploadedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        return document;
    }

    public async Task<FileDocument> UpdateAsync(FileDocument document, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FileDocument>.Filter.Eq(doc => doc.Id, document.Id);
        await _collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
        return document;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FileDocument>.Filter.Eq(doc => doc.Id, id);
        var update = Builders<FileDocument>.Update.Set(doc => doc.IsDeleted, true);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteByObjectKeyAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FileDocument>.Filter.And(
            Builders<FileDocument>.Filter.Eq(doc => doc.BucketName, bucketName),
            Builders<FileDocument>.Filter.Eq(doc => doc.ObjectKey, objectKey)
        );
        var update = Builders<FileDocument>.Update.Set(doc => doc.IsDeleted, true);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<List<FileDocument>> GetFilesByMetadataAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FileDocument>.Filter.And(
            Builders<FileDocument>.Filter.ElemMatch(doc => doc.Metadata, 
                Builders<KeyValuePair<string, string>>.Filter.And(
                    Builders<KeyValuePair<string, string>>.Filter.Eq(kvp => kvp.Key, key),
                    Builders<KeyValuePair<string, string>>.Filter.Eq(kvp => kvp.Value, value)
                )
            ),
            Builders<FileDocument>.Filter.Eq(doc => doc.IsDeleted, false)
        );

        return await _collection
            .Find(filter)
            .SortByDescending(doc => doc.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Récupère tous les fichiers liés à un Sample
    /// </summary>
    public async Task<List<FileDocument>> GetSampleFilesAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        return await GetFilesByMetadataAsync("sample-id", sampleId.ToString(), cancellationToken);
    }
}