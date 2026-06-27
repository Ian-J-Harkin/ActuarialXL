using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using ActuarialTranslationEngine.API.Services;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.Persistence;

namespace ActuarialTranslationEngine.Tests.Unit
{
    public class StartupSweeperTests
    {
        [Fact]
        public void RunDatabaseSweeper_WithOrphanedJobs_MarksThemAsFailed()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ActuarialDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new ActuarialDbContext(options))
            {
                context.TranslationJobs.Add(new TranslationJobEntity
                {
                    Id = Guid.NewGuid(),
                    OriginalFileName = "running.xlsx",
                    Status = TranslationJobStatus.Running,
                    TargetSheet = "Sheet1",
                    ModelUsed = "test"
                });
                context.TranslationJobs.Add(new TranslationJobEntity
                {
                    Id = Guid.NewGuid(),
                    OriginalFileName = "pending.xlsx",
                    Status = TranslationJobStatus.Pending,
                    TargetSheet = "Sheet1",
                    ModelUsed = "test"
                });
                context.TranslationJobs.Add(new TranslationJobEntity
                {
                    Id = Guid.NewGuid(),
                    OriginalFileName = "completed.xlsx",
                    Status = TranslationJobStatus.Completed,
                    TargetSheet = "Sheet1",
                    ModelUsed = "test"
                });
                context.SaveChanges();
            }

            // Act
            using (var context = new ActuarialDbContext(options))
            {
                StartupSweeper.RunDatabaseSweeper(context);
            }

            // Assert
            using (var context = new ActuarialDbContext(options))
            {
                var jobs = context.TranslationJobs.ToList();
                var runningOrphan = jobs.Single(j => j.OriginalFileName == "running.xlsx");
                var pendingOrphan = jobs.Single(j => j.OriginalFileName == "pending.xlsx");
                var completedJob = jobs.Single(j => j.OriginalFileName == "completed.xlsx");

                Assert.Equal(TranslationJobStatus.Failed, runningOrphan.Status);
                Assert.Equal(TranslationJobStatus.Failed, pendingOrphan.Status);
                Assert.Equal(TranslationJobStatus.Completed, completedJob.Status);

                var partitions = context.TranslationPartitions.ToList();
                Assert.Equal(2, partitions.Count);
                Assert.Contains(partitions, p => p.JobId == runningOrphan.Id && p.ErrorMessage == "Job orphaned due to server restart.");
                Assert.Contains(partitions, p => p.JobId == pendingOrphan.Id && p.ErrorMessage == "Job orphaned due to server restart.");
            }
        }

        [Fact]
        public async Task RunDiskSweeper_WithOldAndNewFiles_DeletesOnlyOldFiles()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var oldFile = Path.Combine(tempDir, "old.xlsx");
                var newFile = Path.Combine(tempDir, "new.xlsx");
                var notExcelFile = Path.Combine(tempDir, "old.txt");

                await File.WriteAllTextAsync(oldFile, "test");
                await File.WriteAllTextAsync(newFile, "test");
                await File.WriteAllTextAsync(notExcelFile, "test");

                // Set creation times
                File.SetCreationTimeUtc(oldFile, DateTime.UtcNow.AddHours(-25));
                File.SetCreationTimeUtc(newFile, DateTime.UtcNow.AddHours(-1));
                File.SetCreationTimeUtc(notExcelFile, DateTime.UtcNow.AddHours(-25));

                // Act
                StartupSweeper.RunDiskSweeper(tempDir, DateTime.UtcNow.AddHours(-24));

                // Assert
                Assert.False(File.Exists(oldFile), "Old .xlsx file should be deleted");
                Assert.True(File.Exists(newFile), "New .xlsx file should not be deleted");
                Assert.True(File.Exists(notExcelFile), "Old non-xlsx file should not be deleted");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
