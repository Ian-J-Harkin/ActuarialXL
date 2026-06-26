# What To Do Next

## Current State (Where We Left Off)
We have successfully completed **Phase IX: Real-Time Observability & Live Integration** and **Phase X: Option B (Expand Pipeline to Unsolved Archetypes)**.

The E2E translation pipeline is now **fully functional, mathematically accurate, completely observable, and supports all 4 primary archetypes** across both the Web UI and the CLI.

1. **Mathematical Accuracy (The Triple Lock):** We resolved LLM hallucination variance by isolating context partition-by-partition, strictly enforcing the target column (`targetColumn`) dynamically via `LiveDomainInterrogationBridge`, and forcing `TokenizedFormulaTemplate` look-back syntax in the system prompt.
2. **Contextual Reliability:** We stopped `KeyNotFoundException` crashes by pre-loading historical `[-1]` and future `[+1]` look-ahead rows into the `runningState` dictionary inside the `ReconciliationOrchestrator`.
3. **Observability & UX:** We implemented a generic `IProgress<TranslationProgressEvent>` across the engine. This feeds a live console output stream in the CLI, and streams real-time updates via **SignalR** to an animated Progress Bar and Log Window in the Blazor Web UI!
4. **Archetype Expansion:** We successfully integrated dynamic sheet selection into the UI ("Target Sheet Strategy") and expanded the `VectorCompressionEngine` to automatically handle dynamic formula offsets and look-aheads beyond a simple `[-1]` lookback.

## The Next Step: Candidates

Based on the project trajectory, the logical next phases are:

### Option A: Complete the Blazor Web UI (COMPLETED)
We have successfully built the **Governance Dashboard** and Audit Ledger features.
- We wired up `SqlitePersistenceManager` to securely log all translated partitions.
- We added an **Audit History** interface that retrieves and displays past transactions.
- We introduced **Provenance Badges** tracking the UUID and model, and added browser-based `Download .cs` buttons.
- Furthermore, we solved upstream transient network drops by implementing **Polly Exponential Backoff Retries** inside the API and CLI.

### Option B: Expand the Pipeline to Unsolved Archetypes [COMPLETED]
Right now, the pipeline natively solves `Time-Series` translation (e.g. `18.10` and `13.4`), as well as:
- **Stochastic Modeling**
- **Balancing Ledgers**
- **Variable Adjusters**
  - [x] Added unit tests and E2E tests for these models.
  - [x] Expanded LLM contextual bridging to parse positive and negative offsets (e.g., `Col[+20]`) and dynamically populated the validation dictionary `runningState` so the compiler never throws a `KeyNotFoundException`.

### Option C: E2E Pipeline Refactoring & Cleanup [COMPLETED]
The system has grown rapidly. We successfully:
- Reverted and cleaned up technical debt in the `ReconciliationOrchestrator`.
- Refined the LLM `system-prompt.txt` based on the successful `18.10` patterns.
- Enhanced test coverage across `ActuarialTranslationEngine.API` and `ActuarialTranslationEngine.Web`, securing the infrastructure for 100% reliable concurrent parallel test execution.

## Backlog / UX Enhancements
- **[RESOLVED] Home Screen Routing:** When an audit history exists in the database, the initial Home screen should present a clear choice: "Upload New Model" or "Go Straight to Audit History".
  - [x] TODO: Add unit tests
  - [ ] TODO: Ask user about integration and E2E testing upon completion
- **[RESOLVED] Validation Status Indicators:** The Web UI needs clear visual indicator states for the Automated Reconciliation Loop results, specifically distinguishing between "Certified: Zero Variance" (Green/Pass) and "Reconciliation Failure" (Red/Fail with Variance Delta).
  - [ ] TODO: Add unit tests
  - [ ] TODO: Ask user about integration and E2E testing upon completion
- **[RESOLVED] Disruptive Node Exception Handling:** The UI requires warning banners or Jira-style alerts to surface spreadsheet anomalies (like `#REF!` errors or volatile `RAND()` functions) to the user.
  - [x] TODO: Add unit tests
  - [x] TODO: Ask user about integration and E2E testing upon completion

## Backlog / Technical Debt
- **Security Vulnerabilities: [RESOLVED]** Resolved compiler warnings about out-of-date and vulnerable packages by adding explicit `PackageReference` overrides for the transitive dependencies:
  - `System.Drawing.Common` pinned to 10.0.9 in `Engine.csproj` (was 4.7.0 via ClosedXML — critical vulnerability NU1904 / GHSA-rxg9-xrhp-64gj).
  - `SQLitePCLRaw.bundle_e_sqlite3` pinned to 3.0.3 in `Persistence.csproj` (was 2.1.11 via EF Core Sqlite — high vulnerability NU1903 / GHSA-2m69-gcr7-jv3q).

## Backlog / Resilience & Reliability
These items address gaps discovered during live testing where a 100-second `HttpClient.Timeout` crashed the Web UI with no retry, no logging, and no recovery.

### Immediate Fixes
- **[RESOLVED] Add `TaskCanceledException` to Polly policy (API→LLM).** Added `.Or<TaskCanceledException>()` to the policy so LLM timeouts are retried with backoff.
- **[RESOLVED] Add exception logging to `/api/evaluate` catch block.** Added `ILogger` injection to the Minimal API endpoint and logged the full exception, file name, and elapsed stopwatch time before returning the error response.
- **[RESOLVED] Add timeout/retry handling to Orchestrator.** Added a catch for `OperationCanceledException` to `ReconciliationOrchestrator.RequestTranslationWithRetryAsync` so mid-partition LLM timeouts are retried without crashing the entire job. Added 3 new unit tests to enforce this behavior.

### Short-Term Improvements
- **[RESOLVED] Add Polly retry to Web→API path.** The `ApiTranslationClient` in `Web/Program.cs` now has a resilient `WaitAndRetryAsync` policy configured via `Microsoft.Extensions.Http.Polly` so transient API failures are automatically retried.
- **[RESOLVED] Switch to Async Job Pattern.** The `/api/evaluate` POST endpoint holds the connection open for minutes while it waits for the LLM. It was refactored to an Async Job Pattern (returns `202 Accepted` immediately, processes in `BackgroundTranslationWorker`, and signals completion via SignalR).
- **[RESOLVED] Add structured logging with correlation IDs.** Added a `CorrelationId` generated in the Web `ApiTranslationClient` that is explicitly passed to the API via form data. We wrap the Minimal API endpoint and the `BackgroundTranslationWorker` execution in a `_logger.BeginScope` so that ALL underlying logs (including the LLM Bridge and Roslyn Orchestrator) are tagged with the specific Correlation ID. Configured `IncludeScopes = true` in both `Program.cs` files to surface the tags in console output.

  - **[RESOLVED] Data Provenance Tracking.** The LLM orchestrator now natively injects exact source tags (e.g., `Worksheet Table 13.4`) into the pipeline. These labels persist all the way to the UI, replacing the generic "Partition N" headers with precise auditing context.
### Medium-Term Architectural Changes
- **[RESOLVED] Migrate to One-to-Many Database Schema (Fault Tolerance).** The legacy atomic save pattern was replaced. The database schema now correctly maps a `TranslationJob` to multiple `TranslationPartition` records. Partitions are saved as they complete incrementally, preventing complete data loss during intermittent network or LLM API timeouts.
- **[TODO] Migrate from EnsureCreated() to EF Core Migrations.** The current database initialization relies on `dbContext.Database.EnsureCreated()`, which bootstraps a schema instantly but does not support schema evolution (e.g., adding new columns). This caused crashes during E2E testing when the C# models were updated but the `audit.db` file remained on an old schema. The persistence layer must be transitioned to use `dbContext.Database.Migrate()` and official migration scripts to properly handle data evolution in production.

## Reference Documents
- `docs/project-status-tracker.md` (Phases VIII and IX now marked COMPLETED)
- `docs/Phase9_Walkthrough.md` (Implementation details for Web UI and CLI progress bars)
