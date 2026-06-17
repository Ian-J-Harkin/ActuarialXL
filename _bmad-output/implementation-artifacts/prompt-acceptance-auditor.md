You are an Acceptance Auditor. Review this diff against the spec and context docs. Check for: violations of acceptance criteria, deviations from spec intent, missing implementation of specified behavior, contradictions between spec constraints and actual code. Output findings as a Markdown list. Each finding: one-line title, which AC/constraint it violates, and evidence from the diff.

# Spec:
``markdown
# Story 4.2: ASP.NET Core WebAPI Wrapper

**Status:** ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consuming system,
I want to interact with the engine via RESTful HTTP endpoints,
So that I can submit Excel files and retrieve evaluated logic without needing to reference the internal engine DLLs directly.

## Acceptance Criteria

1. **Given** the running ASP.NET Core WebAPI,
   **When** a client POSTs an actuarial `.xlsx` or `.xlsm` file to the ingestion endpoint,
   **Then** the API must route the file through the extraction and translation pipeline,
   **And** return a `200 OK` containing the serialized evaluation results,
   **And** properly isolate the memory by invoking `AssemblyLoadContext.Unload()` upon request completion to prevent memory leaks.

2. **Given** the running ASP.NET Core WebAPI,
   **When** a client POSTs a malformed payload or an unsupported file type,
   **Then** the API must immediately return a `400 Bad Request` with a clear validation error,
   **And** still properly invoke `AssemblyLoadContext.Unload()` to ensure no orphan memory partitions remain.

## Tasks / Subtasks

- [ ] Task 1: Setup ASP.NET Core WebAPI Project
  - [ ] Create `ActuarialTranslationEngine.API` project (Minimal APIs).
  - [ ] Add project references to `ActuarialTranslationEngine.Engine` and `ActuarialTranslationEngine.Persistence`.
  - [ ] Add to solution `ActuarialTranslationEngine.slnx`.
- [ ] Task 2: Implement Dependency Injection
  - [ ] Register core pipeline services (Extraction, Compression, Reconciliation).
  - [ ] Register `IPersistenceManager` using `builder.Services.AddActuarialPersistence("audit.db")` extension method created in previous story.
- [ ] Task 3: Implement Ingestion Endpoint
  - [ ] Create POST endpoint to accept `.xlsx` / `.xlsm` file uploads.
  - [ ] Implement input validation (file extension, payload integrity).
  - [ ] Implement asynchronous task offloading via channels or thread-throttled queues to protect memory pools.
- [ ] Task 4: Implement Pipeline & Memory Sandboxing
  - [ ] Route the uploaded stream through the extraction, compression, and translation pipeline.
  - [ ] Save the translated payload using `IPersistenceManager`.
  - [ ] Ensure Roslyn compilation is executed within a Collectible `AssemblyLoadContext` (`isCollectible: true`).
  - [ ] Guarantee `AssemblyLoadContext.Unload()` is explicitly invoked (e.g., via `using` or `finally` block) regardless of success or validation failure.
- [ ] Task 5: Unit/Integration Testing
  - [ ] Pull integration traits out of primary test assembly into `ActuarialTranslationEngine.Tests.Integration` (if required by spec) or create basic endpoint tests.
  - [ ] Verify 200 OK path and 400 Bad Request path.

## Dev Agent Guardrails & Technical Context

### Architecture Compliance
- **Minimal APIs:** Use the .NET Minimal API paradigm in `Program.cs`. Do not scaffold traditional MVC Controllers.
- **Concurrency Gates:** Implement asynchronous task offloading (e.g., `System.Threading.Channels` or `SemaphoreSlim`) inside the ingestion controller to protect memory pools from exhaustion under simultaneous large file uploads.
- **Collectible Memory Sandboxing:** Modify the Phase III-B compilation host block (or wrap it) by enabling `isCollectible: true` inside the `AssemblyLoadContext` constructor. Call `isolatedContext.Unload()` on validation loop completion to immediately reclaim server memory allocations.

### Previous Story Intelligence
- **Persistence Integration:** Story 4.1 introduced `ActuarialTranslationEngine.Persistence`. We created `ServiceCollectionExtensions.cs` containing `AddActuarialPersistence(string databasePath)`. You must use this extension to register the DB.
- **Schema Initialization:** We added `EnsureCreated()` to the `SqlitePersistenceManager` constructor in 4.1 to handle schema creation.

### File Structure Requirements
- `ActuarialTranslationEngine.API` (New ASP.NET Core WebAPI Project).
- `Program.cs` - Minimal API endpoint definitions and DI registrations.

### Testing Requirements
- Basic endpoint integration tests using `WebApplicationFactory` to verify file upload endpoints and response codes (200 OK, 400 Bad Request).

## Dev Notes
- Memory leaks are the primary concern here. Ensure the `AssemblyLoadContext` is safely unloaded even if an exception occurs during pipeline execution. Use a `finally` block or custom disposable wrapper if necessary.
- Return the evaluation results as a JSON structure mimicking the models from `ActuarialTranslationEngine.Core.Models`.

---
*Completion note: Ultimate context engine analysis completed - comprehensive developer guide created.*

``

# Diff:
``diff
diff --git a/ActuarialTranslationEngine.CLI/Program.cs b/ActuarialTranslationEngine.CLI/Program.cs
index 40302d5..d66a2af 100644
--- a/ActuarialTranslationEngine.CLI/Program.cs
+++ b/ActuarialTranslationEngine.CLI/Program.cs
@@ -53,8 +53,7 @@ namespace ActuarialTranslationEngine.CLI
                     services.AddSingleton<IActuarialExtractionEngine, ActuarialExtractionEngine>();
                     services.AddSingleton<IVectorCompressionEngine, VectorCompressionEngine>();
                     
-                    // Enforce Phase III-A Boundary Rule (Mock Bridge only)
-                    // Register LLM bridge configuration from env var
+                    // Enforce Phase III-B Boundary Rule: Load Prompt from Governance Log
                     services.AddSingleton<LlmBridgeConfiguration>(provider =>
                     {
                         var cfg = new LlmBridgeConfiguration();
@@ -63,6 +62,35 @@ namespace ActuarialTranslationEngine.CLI
                             cfg.ApiKey = key;
                         else
                             throw new InvalidOperationException("OpenRouter API key not set in environment variable OPENROUTER_API_KEY");
+
+                        // Resolve path to the master prompt log
+                        string relativePath = System.IO.Path.Combine("docs", "governance", "master-prompt-engineering-log.md");
+                        string promptPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), relativePath));
+                        
+                        // Fallback if running from bin/Debug/...
+                        if (!System.IO.File.Exists(promptPath))
+                        {
+                            promptPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", relativePath));
+                        }
+
+                        if (System.IO.File.Exists(promptPath))
+                        {
+                            string logContent = System.IO.File.ReadAllText(promptPath);
+                            var match = System.Text.RegularExpressions.Regex.Match(logContent, @"### The System Prompt\s*```text\s*(.*?)\s*```", System.Text.RegularExpressions.RegexOptions.Singleline);
+                            if (match.Success)
+                            {
+                                cfg.SystemPrompt = match.Groups[1].Value.Trim();
+                            }
+                            else
+                            {
+                                throw new InvalidOperationException("Could not locate the System Prompt text block in the master prompt engineering log.");
+                            }
+                        }
+                        else
+                        {
+                            throw new System.IO.FileNotFoundException($"Prompt engineering log not found at {promptPath}");
+                        }
+
                         return cfg;
                     });
                     
diff --git a/ActuarialTranslationEngine.Core/Interfaces/IReconciliationOrchestrator.cs b/ActuarialTranslationEngine.Core/Interfaces/IReconciliationOrchestrator.cs
index c472ef1..6d2ec93 100644
--- a/ActuarialTranslationEngine.Core/Interfaces/IReconciliationOrchestrator.cs
+++ b/ActuarialTranslationEngine.Core/Interfaces/IReconciliationOrchestrator.cs
@@ -6,5 +6,5 @@ using ActuarialTranslationEngine.Core.Models;
 
 public interface IReconciliationOrchestrator
 {
-    Task ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap workbookMap, CancellationToken cancellationToken = default);
+    Task<List<TranslationOutput>> ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap workbookMap, CancellationToken cancellationToken = default);
 }
diff --git a/ActuarialTranslationEngine.Engine/Orchestration/ReconciliationOrchestrator.cs b/ActuarialTranslationEngine.Engine/Orchestration/ReconciliationOrchestrator.cs
index e5b4c5b..55bbc59 100644
--- a/ActuarialTranslationEngine.Engine/Orchestration/ReconciliationOrchestrator.cs
+++ b/ActuarialTranslationEngine.Engine/Orchestration/ReconciliationOrchestrator.cs
@@ -20,8 +20,9 @@ public class ReconciliationOrchestrator : IReconciliationOrchestrator
         _roslynEngine = roslynEngine ?? throw new ArgumentNullException(nameof(roslynEngine));
     }
 
-    public async Task ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap workbookMap, CancellationToken cancellationToken = default)
+    public async Task<List<TranslationOutput>> ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap workbookMap, CancellationToken cancellationToken = default)
     {
+        var results = new List<TranslationOutput>();
         foreach (var partition in block.Partitions)
         {
             // 1. Identify Target Column
@@ -72,6 +73,7 @@ public class ReconciliationOrchestrator : IReconciliationOrchestrator
                         await _roslynEngine.CompileAndVerifyAsync(csharpCode, inputs, expectedResult, cancellationToken);
                     }
 
+                    results.Add(llmOutput);
                     success = true;
                     break; // Success! Break out of the retry loop.
                 }
@@ -92,6 +94,8 @@ public class ReconciliationOrchestrator : IReconciliationOrchestrator
                 throw new ActuarialDynamicCompilationException("Failed to compile valid C# after 3 attempts.");
             }
         }
+        
+        return results;
     }
 
     private string DetermineTargetColumn(VectorRangePartition partition)
diff --git a/ActuarialTranslationEngine.Tests.Unit/ActuarialTranslationEngine.Tests.Unit.csproj b/ActuarialTranslationEngine.Tests.Unit/ActuarialTranslationEngine.Tests.Unit.csproj
index 3fa488d..4ce440d 100644
--- a/ActuarialTranslationEngine.Tests.Unit/ActuarialTranslationEngine.Tests.Unit.csproj
+++ b/ActuarialTranslationEngine.Tests.Unit/ActuarialTranslationEngine.Tests.Unit.csproj
@@ -10,6 +10,8 @@
   <ItemGroup>
     <PackageReference Include="coverlet.collector" Version="6.0.4" />
     <PackageReference Include="EPPlus" Version="4.5.3.3" />
+    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.9" />
+    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.9" />
     <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
     <PackageReference Include="xunit" Version="2.9.3" />
     <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
@@ -22,6 +24,8 @@
   <ItemGroup>
     <ProjectReference Include="..\ActuarialTranslationEngine.Core\ActuarialTranslationEngine.Core.csproj" />
     <ProjectReference Include="..\ActuarialTranslationEngine.Engine\ActuarialTranslationEngine.Engine.csproj" />
+    <ProjectReference Include="..\ActuarialTranslationEngine.Persistence\ActuarialTranslationEngine.Persistence.csproj" />
+    <ProjectReference Include="..\ActuarialTranslationEngine.API\ActuarialTranslationEngine.API.csproj" />
   </ItemGroup>
 
 </Project>
\ No newline at end of file
diff --git a/ActuarialTranslationEngine.slnx b/ActuarialTranslationEngine.slnx
index e083734..bf817d4 100644
--- a/ActuarialTranslationEngine.slnx
+++ b/ActuarialTranslationEngine.slnx
@@ -1,6 +1,8 @@
 <Solution>
+  <Project Path="ActuarialTranslationEngine.API/ActuarialTranslationEngine.API.csproj" />
   <Project Path="ActuarialTranslationEngine.CLI/ActuarialTranslationEngine.CLI.csproj" />
   <Project Path="ActuarialTranslationEngine.Core/ActuarialTranslationEngine.Core.csproj" />
   <Project Path="ActuarialTranslationEngine.Engine/ActuarialTranslationEngine.Engine.csproj" />
   <Project Path="ActuarialTranslationEngine.Tests.Unit/ActuarialTranslationEngine.Tests.Unit.csproj" />
+  <Project Path="ActuarialTranslationEngine.Persistence/ActuarialTranslationEngine.Persistence.csproj" />
 </Solution>
diff --git a/docs/Project_Journey.md b/docs/Project_Journey.md
index 5b0b976..3a59bed 100644
--- a/docs/Project_Journey.md
+++ b/docs/Project_Journey.md
@@ -40,6 +40,7 @@ Risk considerations for the live bridge are documented in [Phase3B_Risk_Analysis
 - Potential latency and rateGÇælimit impacts when calling OpenRouter.
 - FailureGÇæmode handling for transient network errors (currently a simple retry count; future work will introduce Polly policies).
 - Security review of the API key handling (environment variable vs. configuration file).
+- **Prompt Governance Implementation:** Enforced Phase III-B intent by updating `Program.cs` to dynamically load the LLM system prompt from `docs/governance/master-prompt-engineering-log.md` during DI initialization instead of defaulting to an empty string. This ensures the Live Bridge sends the rigorously tested prompt persona constraints to OpenRouter, maintaining architectural and semantic integrity.
 
 The accompanying spec and decisionGÇælog files provide the concrete implementation guidance:
 - [specGÇæphaseGÇæ3bGÇæliveGÇæbridge/SPEC.md](file:///C:/Github/ActuarialXLpoc/_bmad-output/specs/spec-phase-3b-live-bridge/SPEC.md)
diff --git a/docs/enterprise-lifecycle-spec.md b/docs/enterprise-lifecycle-spec.md
index dc3c841..2afcbd2 100644
--- a/docs/enterprise-lifecycle-spec.md
+++ b/docs/enterprise-lifecycle-spec.md
@@ -155,23 +155,23 @@ namespace ActuarialTranslationEngine.Core.Models
 {
     using System.Collections.Generic;
 
-    public class ColumnDefinition(string columnLetter, string cleanHeaderName, string tokenizedFormulaTemplate, List<int> chronologicalLookbacks)
+    public class ColumnDefinition
     {
-        public string ColumnLetter { get; set; } = columnLetter;
-        public string CleanHeaderName { get; set; } = cleanHeaderName;
-        public string TokenizedFormulaTemplate { get; set; } = tokenizedFormulaTemplate;
-        public List<int> ChronologicalLookbacks { get; set; } = chronologicalLookbacks;
+        public required string ColumnLetter { get; init; }
+        public required string ExtractedHeaderName { get; init; }
+        public string TokenizedFormulaTemplate { get; set; } = string.Empty;
+        public List<int> ChronologicalLookbacks { get; set; } = new();
     }
 
     public class RawWorkbookMap
     {
         public string SheetName { get; set; } = string.Empty;
-        public List<RawRowMetadata> Rows { get; set; } = new();
+        public List<RawRowMetadata> DataRows { get; init; } = new();
     }
 
     public class RawRowMetadata
     {
-        public int RowNumber { get; set; }
+        public int RowIndex { get; set; }
         public Dictionary<string, string> CellFormulas { get; set; } = new(); // Key: ColumnLetter
         public Dictionary<string, string> CellValues { get; set; } = new();   // Key: ColumnLetter
     }
@@ -220,9 +220,11 @@ namespace ActuarialTranslationEngine.Core.Interfaces
         CompressedVectorBlock CompressTopology(RawWorkbookMap rawMap);
     }
 
+    using System.Threading;
+
     public interface IDomainInterrogationBridge
     {
-        Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload);
+        Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string? previousCompilerError = null, CancellationToken cancellationToken = default);
     }
 
     public interface IActuarialReconciliationUnit
@@ -230,9 +232,12 @@ namespace ActuarialTranslationEngine.Core.Interfaces
         decimal ExecuteCalculationRow(Dictionary<string, decimal> inputs);
     }
 
+    // NOTE: Evolved from synchronous to asynchronous (Task-based) in Phase III-B 
+    // to properly support non-blocking syntax tree parsing, compilation steps, 
+    // and cancellation token support during execution timeouts.
     public interface IRoslynReconciliationEngine
     {
-        void CompileAndVerify(string cSharpSourceCode, decimal expectedSpreadsheetResult, Dictionary<string, decimal> rowInputs);
+        Task CompileAndVerifyAsync(string csharpCode, Dictionary<string, decimal> rowInputs, decimal expectedSpreadsheetResult, CancellationToken cancellationToken = default);
     }
 }
 ```
diff --git a/docs/project-status-tracker.md b/docs/project-status-tracker.md
index 780e141..2dffc90 100644
--- a/docs/project-status-tracker.md
+++ b/docs/project-status-tracker.md
@@ -48,7 +48,7 @@ This document tracks the execution and completion status of the phases defined i
 ---
 
 ## Phase III-B: Live Bridge & Reconciliation Validation
-**Status:** GÅ¦ **PENDING**
+**Status:** G£à **COMPLETED**
 * **Goal:** Orchestrate the LLM to write stateless C# strings, execute them dynamically via an in-memory Roslyn compiler, and test the outputs against the raw spreadsheet data (Penny-Perfect Balance).
 * **Reference:** [Phase III-B Detailed Design](detailed-design/phase-3b-live-bridge-and-reconciliation.md)
 
@@ -57,4 +57,12 @@ This document tracks the execution and completion status of the phases defined i
 ## Phase IV: Persistence & Storage Layer (Database Metadata Pattern)
 **Status:** GÅ¦ **PENDING**
 * **Goal:** Implement the embedded `SQLite` database using `Entity Framework Core` to store the versioned C# code and Auditable Markdown payloads in `JSONB` format for hot-reloading.
+* **Requirement:** Must be defined as vertically-sliced BDD user stories before implementation begins.
 * **Reference:** [Architectural Blueprint: Section 8](../architectural-blueprint.md#8-persistence--storage-layer-the-hybrid-database-metadata-pattern)
+
+---
+
+## Phase V: Enterprise API & Governance Sandboxing
+**Status:** GÅ¦ **PENDING**
+* **Goal:** Expose the parsing, compression, and reconciliation core as an ASP.NET Core WebAPI (FR10). Implement asynchronous task offloading via channels (NFR3) and Collectible Memory Sandboxing (NFR4) for high-throughput multi-tenant processing (NFR2).
+* **Requirement:** Must be defined as vertically-sliced BDD user stories before implementation begins.

``
