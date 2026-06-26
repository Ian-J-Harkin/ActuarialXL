using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.Persistence;

namespace DebugScript
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var options = new DbContextOptionsBuilder<ActuarialDbContext>()
                    .UseSqlite("Data Source=test.db")
                    .Options;

                using var dbContext = new ActuarialDbContext(options);
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                var job = new TranslationJobEntity
                {
                    Id = Guid.NewGuid(),
                    OriginalFileName = "test.xlsx",
                    FileHash = "hash",
                    ModelUsed = "Live Model",
                    TargetSheet = "ALL",
                    WorkbookSessionId = Guid.NewGuid(),
                    Status = TranslationJobStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.TranslationJobs.Add(job);
                await dbContext.SaveChangesAsync();
                
                Console.WriteLine("Success!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("INNER: " + ex.InnerException.Message);
                }
            }
        }
    }
}
