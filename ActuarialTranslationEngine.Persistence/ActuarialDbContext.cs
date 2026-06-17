using ActuarialTranslationEngine.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ActuarialTranslationEngine.Persistence;

public class ActuarialDbContext : DbContext
{
    public DbSet<TranslatedModelRecord> TranslatedModels { get; set; }

    public ActuarialDbContext(DbContextOptions<ActuarialDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TranslatedModelRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.OwnsOne(e => e.Payload, builder =>
            {
                builder.ToJson();
                builder.OwnsOne(p => p.Output);
            });
        });
    }
}
