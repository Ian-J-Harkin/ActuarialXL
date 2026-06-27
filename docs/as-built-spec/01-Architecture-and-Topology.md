# 01 Architecture and Topology

The platform implements a multi-project Clean Architecture topology spanning `.NET 10.0`, isolating persistence logic from extraction constraints and generative AI boundaries.

## 1. Solution Assembly Structure
* **`ActuarialTranslationEngine.Core`** (Class Library): Contains interfaces, exceptions, DTOs, and Entity Framework mapping schemas (e.g., `TranslationJobEntity`, `TranslationPartitionEntity`, `CompressedVectorBlock`).
* **`ActuarialTranslationEngine.Persistence`** (Class Library): Contains the `ActuarialDbContext`, `SqlitePersistenceManager`, and EF Core Migration implementations for the underlying `.db` ledger.
* **`ActuarialTranslationEngine.Engine`** (Class Library): Contains the hard logic—`ClosedXML` formula parsing, `EPPlus` VBA extraction, `VectorCompressionEngine` chunking, and the `RoslynReconciliationEngine` dynamic compilation host.
* **`ActuarialTranslationEngine.API`** (ASP.NET Core WebAPI): The central execution gateway. Implements the `BackgroundTranslationWorker`, `LlmWatchdogService`, `StartupSweeper`, and Minimal API Endpoints (Session, Evaluate, History).
* **`ActuarialTranslationEngine.Web`** (Blazor Interactive Server): The Governance UI. Implements SignalR integration (`TranslationProgressHub`), interactive wizards, and ledger visibility.
* **`ActuarialTranslationEngine.Tests.Unit` & `Tests.E2E`**: xUnit and Playwright automated testing layers.

## 2. Infrastructure Dependencies
* **LLM Engine:** `mistralai/codestral-2508` via standard OpenAI REST schemas.
* **Database:** Embedded `SQLite`.
* **Execution Framework:** `Microsoft.CodeAnalysis.CSharp` (Roslyn).
* **Network Resilience:** `Microsoft.Extensions.Http.Polly` (Exponential backoff & timeout trapping).

## 3. High-Level Data Flow
1. **Ingestion:** User uploads `.xlsx`/`.xlsm`. The API writes the file stream to `/uploads` and returns a `SessionId`.
2. **Enqueue:** The job enters the asynchronous `ITranslationJobQueue`.
3. **Extraction & Compression:** The `BackgroundTranslationWorker` triggers the `VectorCompressionEngine` to parse and partition the exact target sheet.
4. **LLM Orchestration:** `ReconciliationOrchestrator` feeds each partition sequentially to the LLM. 
5. **Compilation & Execution:** Roslyn dynamically compiles the resulting C#, testing mathematical assertions against ground-truth.
6. **Persistence:** `SqlitePersistenceManager` incrementally records the successful `TranslationPartitionEntity`.
7. **Observability:** SignalR streams progress states to the Blazor client in real-time.
