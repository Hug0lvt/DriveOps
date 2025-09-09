using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using DriveOps.Shared.Models.Files;

namespace DriveOps.Infrastructure.Features.Files.Configurations;

public static class FileDocumentMongoConfig
{
    public static void Configure()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(FileDocument)))
        {
            BsonClassMap.RegisterClassMap<FileDocument>(classMap =>
            {
                classMap.AutoMap();
                
                // Configuration de l'Id comme _id MongoDB
                classMap.MapIdMember(x => x.Id)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                
                // Configuration des propriétés avec noms MongoDB
                classMap.MapMember(x => x.FileName)
                    .SetElementName("fileName");
                    
                classMap.MapMember(x => x.OriginalFileName)
                    .SetElementName("originalFileName");
                    
                classMap.MapMember(x => x.ContentType)
                    .SetElementName("contentType");
                    
                classMap.MapMember(x => x.FileSize)
                    .SetElementName("fileSize");
                    
                classMap.MapMember(x => x.BucketName)
                    .SetElementName("bucketName");
                    
                classMap.MapMember(x => x.ObjectKey)
                    .SetElementName("objectKey");
                    
                classMap.MapMember(x => x.Checksum)
                    .SetElementName("checksum");
                    
                classMap.MapMember(x => x.UploadedAt)
                    .SetElementName("uploadedAt");
                    
                classMap.MapMember(x => x.UploadedBy)
                    .SetElementName("uploadedBy");
                    
                classMap.MapMember(x => x.Tags)
                    .SetElementName("tags");
                    
                classMap.MapMember(x => x.Metadata)
                    .SetElementName("metadata");
                    
                classMap.MapMember(x => x.IsDeleted)
                    .SetElementName("isDeleted");
            });
        }
    }
}