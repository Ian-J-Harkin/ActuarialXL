using ActuarialTranslationEngine.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ActuarialTranslationEngine.Persistence;

public class ActuarialDbContext : DbContext
{
    public DbSet<TranslationJobEntity> TranslationJobs { get; set; }
    public DbSet<TranslationPartitionEntity> TranslationPartitions { get; set; }

    public ActuarialDbContext(DbContextOptions<ActuarialDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TranslationJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasMany(e => e.Partitions)
                  .WithOne(p => p.Job)
                  .HasForeignKey(p => p.JobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TranslationPartitionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}
