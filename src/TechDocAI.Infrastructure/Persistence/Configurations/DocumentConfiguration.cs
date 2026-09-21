using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechDocAI.Core.Entities;

namespace TechDocAI.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.ContentHash);
        builder.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
    }
}
