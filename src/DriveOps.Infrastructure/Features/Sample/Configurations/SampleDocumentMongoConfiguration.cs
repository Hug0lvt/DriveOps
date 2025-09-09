using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using DriveOps.Shared.Models.Sample;

namespace DriveOps.Infrastructure.Features.Sample.Configurations;

public static class SampleDocumentMongoConfiguration
{
    public static void Configure()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(SampleDocument)))
        {
            BsonClassMap.RegisterClassMap<SampleDocument>(classMap =>
            {
                classMap.AutoMap();
                
                // Configuration de l'Id comme _id MongoDB
                classMap.MapIdMember(x => x.Id)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                
                // Configuration des propriétés avec noms MongoDB
                classMap.MapMember(x => x.SampleId)
                    .SetElementName("sampleId")
                    .SetSerializer(new GuidSerializer(BsonType.String));
                    
                classMap.MapMember(x => x.Version)
                    .SetElementName("version");
                    
                classMap.MapMember(x => x.Data)
                    .SetElementName("data");
                    
                classMap.MapMember(x => x.CreatedAt)
                    .SetElementName("createdAt");
                    
                classMap.MapMember(x => x.UpdatedAt)
                    .SetElementName("updatedAt");
                    
                classMap.MapMember(x => x.CreatedBy)
                    .SetElementName("createdBy");
                    
                classMap.MapMember(x => x.Tags)
                    .SetElementName("tags");
                    
                classMap.MapMember(x => x.IsActive)
                    .SetElementName("isActive");
            });
        }
    }
}