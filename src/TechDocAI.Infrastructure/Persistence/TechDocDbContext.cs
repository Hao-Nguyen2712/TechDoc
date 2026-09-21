using Microsoft.EntityFrameworkCore;
using TechDocAI.Core.Entities;

namespace TechDocAI.Infrastructure.Persistence;

public class TechDocDbContext : DbContext
{
    public TechDocDbContext(DbContextOptions<TechDocDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<Chunk> Chunks => Set<Chunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TechDocDbContext).Assembly);
    }
}
