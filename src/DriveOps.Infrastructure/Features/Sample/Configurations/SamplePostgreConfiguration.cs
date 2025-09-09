using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriveOps.Infrastructure.Features.Sample.Configurations;

public class SamplePostgreConfiguration : IEntityTypeConfiguration<Shared.Models.Sample.Sample>
{
    public void Configure(EntityTypeBuilder<Shared.Models.Sample.Sample> builder)
    {
        builder.ToTable("Samples");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.HasIndex(s => s.Code)
            .IsUnique();
            
        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();
            
        builder.Property(s => s.CreatedAt)
            .IsRequired();
            
        builder.Property(s => s.UpdatedAt)
            .IsRequired();
            
        builder.Property(s => s.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(s => s.UpdatedBy)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Configuration du sous-objet SampleMetadata comme owned entity
        builder.OwnsOne(s => s.Metadata, metadata =>
        {
            metadata.Property(m => m.Title)
                .HasMaxLength(300);
                
            metadata.Property(m => m.Description)
                .HasMaxLength(1000);
                
            metadata.Property(m => m.Category)
                .HasMaxLength(100);
                
            metadata.Property(m => m.Priority)
                .IsRequired();
        });

        // Configuration de la liste des tags
        builder.OwnsMany(s => s.Tags, tag =>
        {
            tag.WithOwner().HasForeignKey("SampleId");
            tag.Property<Guid>("SampleId");
            
            tag.HasKey(t => t.Id);
            
            tag.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(100);
                
            tag.Property(t => t.Color)
                .HasMaxLength(7); // Pour les codes couleur hex #FFFFFF
                
            tag.Property(t => t.CreatedAt)
                .IsRequired();
        });
    }
}