# Story 4.2: ASP.NET Core WebAPI Wrapper

**Status:** done

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

### Review Findings

- [x] [Review][Patch] Missing API Project and Implementation Files in the Diff — The diff registers the API project in the solution but does not contain the actual project files or endpoint code. Are we reviewing an incomplete diff?
- [x] [Review][Patch] Breaking changes to core model properties — Renaming properties (e.g., `CleanHeaderName` to `ExtractedHeaderName`) and removing the primary constructor from `ColumnDefinition` breaks serialization and compile-time compatibility.
- [x] [Review][Patch] API config depends on docs folder — Loading LLM prompt from `docs/governance/master-prompt-engineering-log.md` is brittle.
- [x] [Review][Patch] `IDomainInterrogationBridge` leaks compiler concerns — Adding `previousCompilerError` to interface couples it to Roslyn and breaks implementers.
- [x] [Review][Patch] Absence of Asynchronous Task Offloading via Channels — Uses `SemaphoreSlim` inline instead of offloading heavy Roslyn compilation to background channels as requested in Task 3.
- [x] [Review][Patch] Brittle fallback path traversal [`Program.cs`]
- [x] [Review][Patch] Fragile System Prompt Regex parsing [`Program.cs`]
- [x] [Review][Patch] Blocking I/O in DI registration [`Program.cs`]
- [x] [Review][Patch] Lack of logging on missing prompt file [`Program.cs`]
- [x] [Review][Patch] `ReconciliationOrchestrator` compilation loop discards successful results on failure [`ReconciliationOrchestrator.cs`]
- [x] [Review][Patch] Missing null checks in Orchestrator [`ReconciliationOrchestrator.cs`]
- [x] [Review][Patch] Missing cancellation token check in Orchestrator loop [`ReconciliationOrchestrator.cs`]
- [x] [Review][Patch] Incomplete Persistence of Translated Results [`Program.cs`]
- [x] [Review][Patch] Roslyn Compilation Performed Outside Collectible Context [`RoslynReconciliationEngine.cs`]
- [x] [Review][Defer] Unit test project polluted with integration dependencies [`ActuarialTranslationEngine.Tests.Unit.csproj`] — deferred, pre-existing
- [x] [Review][Defer] Missing Test Implementations in Unit Test Modifications [`ActuarialTranslationEngine.Tests.Unit.csproj`] — deferred, pre-existing

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
