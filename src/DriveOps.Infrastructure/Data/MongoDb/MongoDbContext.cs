using MongoDB.Driver;
using DriveOps.Shared.Models.Sample;
using DriveOps.Shared.Models.Files;
using DriveOps.Infrastructure.Features.Sample.Configurations;
using DriveOps.Infrastructure.Features.Files.Configurations;

namespace DriveOps.Infrastructure.Data.MongoDb;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private static bool _configured = false;
    
    public MongoDbContext(IMongoClient mongoClient, string databaseName)
    {
        _database = mongoClient.GetDatabase(databaseName);
        
        // Initialiser les configurations MongoDB une seule fois
        if (!_configured)
        {
            ConfigureModels();
            _configured = true;
        }
    }

    public IMongoCollection<SampleDocument> SampleDocuments => 
        _database.GetCollection<SampleDocument>("sampleDocuments");
        
    public IMongoCollection<FileDocument> FileDocuments => 
        _database.GetCollection<FileDocument>("fileDocuments");
        
    private static void ConfigureModels()
    {
        SampleDocumentMongoConfiguration.Configure();
        FileDocumentMongoConfig.Configure();
    }
}