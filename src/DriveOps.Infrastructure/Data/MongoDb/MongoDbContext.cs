using MongoDB.Driver;
using DriveOps.Shared.Models.Sample;
using DriveOps.Infrastructure.Features.Sample.Configurations;

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
        
    private static void ConfigureModels()
    {
        SampleDocumentMongoConfig.Configure();
        // On peut ajouter d'autres configurations ici
    }
}