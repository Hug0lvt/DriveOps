using DriveOps.Infrastructure.Features.Sample.Repositories;

namespace DriveOps.Infrastructure.Data.MongoDb;

public class MongoDbUnitOfWork
{
    private readonly MongoDbContext _mongoContext;
    private SampleDocumentMongoRepo? _sampleDocumentRepository;

    public MongoDbUnitOfWork(MongoDbContext mongoContext)
    {
        _mongoContext = mongoContext;
    }
        
    public SampleDocumentMongoRepo SampleDocumentRepository => 
        _sampleDocumentRepository ??= new SampleDocumentMongoRepo(_mongoContext);
}