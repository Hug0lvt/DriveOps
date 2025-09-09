using MongoDB.Driver;
using MongoDB.Bson;
using DriveOps.Shared.Models.Sample;
using DriveOps.Infrastructure.Data.MongoDb;

namespace DriveOps.Infrastructure.Features.Sample.Repositories;

public class SampleDocumentMongoRepo
{
    private readonly MongoDbContext _context;
    private readonly IMongoCollection<SampleDocument> _collection;

    public SampleDocumentMongoRepo(MongoDbContext context)
    {
        _context = context;
        _collection = context.SampleDocuments;
    }

    public async Task<SampleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(doc => doc.Id == id && doc.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SampleDocument?> GetBySampleIdAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(doc => doc.SampleId == sampleId && doc.IsActive)
            .SortByDescending(doc => doc.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<SampleDocument>> GetBySampleIdAllVersionsAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(doc => doc.SampleId == sampleId)
            .SortByDescending(doc => doc.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<SampleDocument> CreateAsync(SampleDocument document, CancellationToken cancellationToken = default)
    {
        document.Id = Guid.NewGuid();
        document.CreatedAt = DateTime.UtcNow;
        document.UpdatedAt = DateTime.UtcNow;
        
        // Obtenir la dernière version pour ce sampleId
        var lastVersion = await GetLatestVersionAsync(document.SampleId, cancellationToken);
        document.Version = lastVersion + 1;
        
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        return document;
    }

    public async Task<SampleDocument> SaveJsonAsync(Guid sampleId, object jsonData, string createdBy, List<string>? tags = null, CancellationToken cancellationToken = default)
    {
        var document = new SampleDocument
        {
            SampleId = sampleId,
            Data = jsonData.ToBsonDocument(),
            CreatedBy = createdBy,
            Tags = tags ?? new List<string>()
        };

        return await CreateAsync(document, cancellationToken);
    }

    public async Task<SampleDocument> UpdateJsonAsync(Guid sampleId, object jsonData, string updatedBy, List<string>? tags = null, CancellationToken cancellationToken = default)
    {
        // Désactiver l'ancienne version
        await DeactivateCurrentVersionAsync(sampleId, cancellationToken);
        
        // Créer une nouvelle version
        var document = new SampleDocument
        {
            SampleId = sampleId,
            Data = jsonData.ToBsonDocument(),
            CreatedBy = updatedBy,
            Tags = tags ?? new List<string>()
        };

        return await CreateAsync(document, cancellationToken);
    }

    public async Task<List<SampleDocument>> SearchByTagsAsync(List<string> tags, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SampleDocument>.Filter.And(
            Builders<SampleDocument>.Filter.AnyIn(doc => doc.Tags, tags),
            Builders<SampleDocument>.Filter.Eq(doc => doc.IsActive, true)
        );

        return await _collection
            .Find(filter)
            .SortByDescending(doc => doc.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SampleDocument>.Filter.Eq(doc => doc.Id, id);
        var update = Builders<SampleDocument>.Update
            .Set(doc => doc.IsActive, false)
            .Set(doc => doc.UpdatedAt, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    private async Task<int> GetLatestVersionAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        var latestDoc = await _collection
            .Find(doc => doc.SampleId == sampleId)
            .SortByDescending(doc => doc.Version)
            .FirstOrDefaultAsync(cancellationToken);
            
        return latestDoc?.Version ?? 0;
    }

    private async Task DeactivateCurrentVersionAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SampleDocument>.Filter.And(
            Builders<SampleDocument>.Filter.Eq(doc => doc.SampleId, sampleId),
            Builders<SampleDocument>.Filter.Eq(doc => doc.IsActive, true)
        );
        
        var update = Builders<SampleDocument>.Update
            .Set(doc => doc.IsActive, false)
            .Set(doc => doc.UpdatedAt, DateTime.UtcNow);

        await _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }
}