using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechDocAI.Core.Entities;

namespace TechDocAI.Infrastructure.Persistence.Configurations;

public class ChunkConfiguration : IEntityTypeConfiguration<Chunk>
{
    public void Configure(EntityTypeBuilder<Chunk> builder)
    {
        builder.ToTable("chunks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Text).IsRequired();
        builder.Property(e => e.PageIndex).IsRequired(false);
        builder.Property(e => e.StartLine).IsRequired(false);
        builder.Property(e => e.EndLine).IsRequired(false);
        builder.Property(e => e.HeadingPath).HasMaxLength(500);
        builder.Property(e => e.EmbeddingModel).HasMaxLength(100);
        builder.Property(e => e.EmbeddingDimensions);
        builder.HasOne(e => e.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
