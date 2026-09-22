using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechDocAI.Core.Entities;

namespace TechDocAI.Infrastructure.Persistence.Configurations;

public class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> builder)
    {
        builder.ToTable("ingestion_jobs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired();
        builder.HasOne(e => e.Document)
            .WithMany(d => d.IngestionJobs)
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
