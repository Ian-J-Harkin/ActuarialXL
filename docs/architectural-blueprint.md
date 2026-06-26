# Architectural Blueprint: Actuarial Semantic Translation Engine

This document defines the overarching architecture and technical design constraints for the Actuarial Semantic Translation Engine. It governs the structural translation of proprietary `.xlsx` models into reproducible, sandboxed C# code.

## 1. System Context & Goals
The system is a headless execution engine designed to extract, compress, and conceptually translate complex actuarial spreadsheet logic into deterministic, penny-perfect C# subroutines using an LLM bridge.

## 2. Core Archetypes
The system supports four distinct calculation archetypes:
1. **Time-Series (Recursive Roll-Forward):** Vectors with horizontal historical recursion.
2. **Stochastic:** Monte Carlo style iterative calculations.
3. **Balancing Ledgers:** Multi-component cross-dependent formulas.
4. **Variable Adjusters:** Standalone single-cell scaling variables.

## 3. The Extraction Layer
- **Technology:** `ClosedXML` (for standard parsing) and `EPPlus` / `DocumentFormat.OpenXml` (for bypassing MS-OVBA compression and reading `.vbaProject.bin`).
- **Function:** Parses spatial maps, handles Volatile functions (e.g., `RAND()`) and native errors (e.g., `#DIV/0!`) via `DisruptiveNodes`.

## 4. The Compression Layer
- **Mechanism:** Implements a continuous change-point algorithm using an explicit column key-sorted array join.
- **Function:** Collapses 60+ row vertical data blocks into 2-3 logical partition blocks (Vector Chunking).

## 5. Domain Interrogation Bridge (LLM Layer)
- **Model Target:** `mistralai/codestral-2508` via OpenRouter.
- **Protocol:** API requests are fully deterministic (Temperature = 0.0).
- **Structure:** Parses the OpenAI-compatible response and splits on the `===CSHARP_MIRROR===` delimiter to extract the translated `IActuarialReconciliationUnit` class.

## 6. Dynamic Compilation & Sandboxing
- **Technology:** `Microsoft.CodeAnalysis.CSharp` (Roslyn).
- **Security:** Managed by the `AstSafetyScanner`, which blocks `System.IO`, `System.Net`, etc.
- **Memory Management:** Implements Collectible Memory Sandboxing via `AssemblyLoadContext` with `isCollectible: true` and forced `Unload()` upon validation completion.

## 7. The Reconciliation Orchestrator
- **Execution:** Runs the generated assembly against 3 representative rows per partition (First, Mid, Last).
- **Validation:** Enforces a strict Variance Delta $\le 0.00001m$. Any failure triggers an `ActuarialLogicLeakException` and re-prompting.

## 8. Persistence & Storage Layer (The Hybrid Database Metadata Pattern)
- **Technology:** `Entity Framework Core` backing an embedded `SQLite` database.
- **Function:** Stores versioned, successfully reconciled C# code logic and accompanying Auditable Markdown payloads. 
- **Fault Tolerance:** Implements a One-to-Many relational schema mapping a single `TranslationJob` to multiple `TranslationPartition` records. This ensures incremental saves, preventing data loss during network interruptions or API timeouts. (Transitioning from `EnsureCreated()` to EF Core Migrations).

## 9. Enterprise Production API & Resilience
- **Technology:** ASP.NET Core WebAPI with `System.Threading.Channels`.
- **Execution Model (Async Job Pattern):** Uses asynchronous background offloading. The API accepts requests (returning `202 Accepted`), and processes jobs in a `BackgroundTranslationWorker` to safely manage memory pools and HTTP connection limits.
- **Resilience:** Implements `Microsoft.Extensions.Http.Polly` for exponential backoff and retry policies across both API-to-LLM and Web-to-API boundaries, specifically catching `TaskCanceledException` and `OperationCanceledException` to recover from transient network drops without failing the entire job.

## 10. Web UI & Real-Time Observability
- **Technology:** Blazor Web App with **SignalR**.
- **Observability:** Leverages a generic `IProgress<TranslationProgressEvent>` interface that broadcasts real-time execution states (e.g., "Translating Column D", "Reconciliation Passed") from the Roslyn Orchestrator to an animated Interactive Translation Wizard on the front-end.
- **Governance:** Includes a full Audit Ledger interface that retrieves historical transactions with Provenance Badges (UUIDs, model versions, and source sheet tags), allowing instant C# code downloads directly from the browser.
