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
- **Function:** Stores versioned, successfully reconciled C# code logic and accompanying Auditable Markdown payloads natively as `JSONB`. This allows for high-speed hot-reloading without re-triggering LLM compilation.

## 9. Enterprise Production API
- **Technology:** ASP.NET Core WebAPI.
- **Concurrency:** Uses asynchronous task offloading (channels or thread-throttled queues) to safely manage memory pools under high-throughput, multi-tenant loads (50MB+ file processing).
