using DriveOps.Infrastructure.Features.Sample.Repositories;
using DriveOps.Infrastructure.Features.Files.Repositories;

namespace DriveOps.Infrastructure.Data.MongoDb;

public class MongoDbUnitOfWork
{
    private readonly MongoDbContext _mongoContext;
    private SampleDocumentMongoRepository? _sampleDocumentRepository;
    private FileDocumentMongoRepository? _fileDocumentRepository;

    public MongoDbUnitOfWork(MongoDbContext mongoContext)
    {
        _mongoContext = mongoContext;
    }
        
    public SampleDocumentMongoRepository SampleDocumentRepository => 
        _sampleDocumentRepository ??= new SampleDocumentMongoRepository(_mongoContext);
        
    public FileDocumentMongoRepository FileDocumentRepository => 
        _fileDocumentRepository ??= new FileDocumentMongoRepository(_mongoContext);
}