# Persistence Layer Specification: Phase IV Actuarial Rules Database

## 1. Architectural Intent

The database must accomplish three enterprise goals:

1. **Versioning & Auditability:** Actuarial models change. Every time the LLM generates a new specification for a sheet, it must be saved as a new version. Old versions are retained for regulatory audit trails.
2. **Schema-less Rule Storage:** The actual output (Markdown + C#) is stored in a single JSONB column. This prevents us from having to run complex database migrations every time the shape of a rule changes.
3. **Hot-Reloading:** The WebAPI can query the "Active" JSON payload, extract the C# string, and feed it into the Roslyn compiler without requiring a system restart or application deployment.

---

## 2. Entity Framework Core Models (C#)

These are the strict domain entities the agent must scaffold inside `ActuarialTranslationEngine.Core/Entities`. We use `Guid` for primary keys to ensure cloud-native microservice compatibility.

```csharp
namespace ActuarialTranslationEngine.Core.Entities
{
    using System;
    using System.Collections.Generic;

    public class ActuarialWorkbook
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<WorksheetTopology> Worksheets { get; set; } = new List<WorksheetTopology>();
    }

    public class WorksheetTopology
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid WorkbookId { get; set; }
        public string SheetName { get; set; } = string.Empty;
        public string ClassificationArchetype { get; set; } = string.Empty; // e.g., "Archetype A"
        
        // Navigation
        public ActuarialWorkbook Workbook { get; set; } = null!;
        public ICollection<RuleTranslationVersion> Translations { get; set; } = new List<RuleTranslationVersion>();
    }

    public class RuleTranslationVersion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid WorksheetId { get; set; }
        
        public int Version { get; set; }
        public bool IsActive { get; set; } // Only ONE version per worksheet can be active
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public string GeneratedByLlmModel { get; set; } = string.Empty; // Audit log of which AI built it

        // EF Core 8/9 JSONB Mapping Target
        public TranslationPayload Payload { get; set; } = new();

        // Navigation
        public WorksheetTopology Worksheet { get; set; } = null!;
    }

    // This class is strictly mapped as a JSON column, not a separate SQL table
    public class TranslationPayload
    {
        public string FinalAuditableMarkdown { get; set; } = string.Empty;
        public string GeneratedCSharpMirrorCode { get; set; } = string.Empty;
        public string ExecutionChecksum { get; set; } = string.Empty; // SHA256 hash of the C# code for tampering validation
    }
}
```

---

## 3. The `DbContext` & JSON Mapping Configuration

To utilize SQLite's JSON capabilities natively in .NET 8/9, the `DbContext` must explicitly map the `TranslationPayload` using the `.ToJson()` method inside the Fluent API. This tells EF Core to serialize the object into a single text column rather than attempting to create foreign key relationships.

```csharp
namespace ActuarialTranslationEngine.Engine.Data
{
    using Microsoft.EntityFrameworkCore;
    using ActuarialTranslationEngine.Core.Entities;

    public class ActuarialGovernanceDbContext : DbContext
    {
        public ActuarialGovernanceDbContext(DbContextOptions<ActuarialGovernanceDbContext> options) : base(options) { }

        public DbSet<ActuarialWorkbook> Workbooks { get; set; }
        public DbSet<WorksheetTopology> Worksheets { get; set; }
        public DbSet<RuleTranslationVersion> RuleTranslations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 0. Sync SQLite Data Types for Guid Primary Keys
            // explicitly add a global value converter mapping Guid to string conversions natively
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?));
                
                foreach (var property in properties)
                {
                    modelBuilder.Entity(entityType.Name)
                        .Property(property.Name)
                        .HasConversion<string>();
                }
            }

            // 1. Configure Workbook -> Worksheet Relationship
            modelBuilder.Entity<WorksheetTopology>()
                .HasOne(w => w.Workbook)
                .WithMany(wb => wb.Worksheets)
                .HasForeignKey(w => w.WorkbookId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Configure Worksheet -> Translations Relationship
            modelBuilder.Entity<RuleTranslationVersion>()
                .HasOne(rt => rt.Worksheet)
                .WithMany(w => w.Translations)
                .HasForeignKey(rt => rt.WorksheetId)
                .OnDelete(DeleteBehavior.Cascade);

            // 3. THE HYBRID JSONB MAPPING
            modelBuilder.Entity<RuleTranslationVersion>()
                .OwnsOne(rt => rt.Payload, payloadBuilder =>
                {
                    payloadBuilder.ToJson(); // Explicitly maps this object to a SQLite JSON column
                });

            // 4. Indexing for Fast Runtime Resolution
            modelBuilder.Entity<RuleTranslationVersion>()
                .HasIndex(rt => new { rt.WorksheetId, rt.IsActive });
        }
    }
}
```

---

## 4. The Resulting SQLite DDL (Raw Schema)

When EF Core generates the migrations, it will create the following highly optimized SQLite schema. Note how the `Payload` is stored natively as `TEXT` (which SQLite uses to enforce JSON via its native JSON1 extension).

```sql
CREATE TABLE "Workbooks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbooks" PRIMARY KEY,
    "FileName" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "UploadedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Worksheets" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Worksheets" PRIMARY KEY,
    "WorkbookId" TEXT NOT NULL,
    "SheetName" TEXT NOT NULL,
    "ClassificationArchetype" TEXT NOT NULL,
    CONSTRAINT "FK_Worksheets_Workbooks_WorkbookId" FOREIGN KEY ("WorkbookId") REFERENCES "Workbooks" ("Id") ON DELETE CASCADE
);

CREATE TABLE "RuleTranslations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_RuleTranslations" PRIMARY KEY,
    "WorksheetId" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "GeneratedAtUtc" TEXT NOT NULL,
    "GeneratedByLlmModel" TEXT NOT NULL,
    "Payload" TEXT NOT NULL, /* JSON DATA STORED HERE */
    CONSTRAINT "FK_RuleTranslations_Worksheets_WorksheetId" FOREIGN KEY ("WorksheetId") REFERENCES "Worksheets" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_RuleTranslations_WorksheetId_IsActive" ON "RuleTranslations" ("WorksheetId", "IsActive");
CREATE INDEX "IX_Worksheets_WorkbookId" ON "Worksheets" ("WorkbookId");
```

---

## 5. Runtime Resolution Pattern (How the API Uses This)

When the WebAPI receives a request to run a calculation, it does **not** invoke the LLM. It executes this fast, sub-millisecond query to pull the active code into the Roslyn sandbox:

```csharp
public async Task<decimal> ExecuteActiveRuleAsync(Guid worksheetId, Dictionary<string, decimal> rowInputs)
{
    // 1. Fetch the active translation payload natively deserialized by EF Core
    var activeRule = await _dbContext.RuleTranslations
        .Where(rt => rt.WorksheetId == worksheetId && rt.IsActive == true)
        .Select(rt => rt.Payload.GeneratedCSharpMirrorCode)
        .SingleOrDefaultAsync();

    if (string.IsNullOrEmpty(activeRule))
        throw new ApplicationException("No active actuarial rule found for this worksheet.");

    // 2. Pass the retrieved C# string directly to the Phase III-B Roslyn Compiler
    return _roslynEngine.CompileAndExecute(activeRule, rowInputs);
}
```
