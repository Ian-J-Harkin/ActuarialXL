# Design vs. Reality: Assessment & Critique

This document provides a critical assessment of the original theoretical architecture (as outlined in `architectural-blueprint.md` and `enterprise-lifecycle-spec.md`) versus the structural reality of implementing the Actuarial Semantic Translation Engine in practice. 

---

## 1. Memory Management & Ingestion Strategies
### ❌ Original Design 
The specification assumed that workbooks (up to 50MB) could be handled natively via `MemoryStream` injections into `ClosedXML` and `OpenXml`. The background worker was expected to simply hold the byte array in memory during translation.

### ✅ Reality (The OOM Crash)
Loading massive legacy actuarial files into memory proved catastrophic. Under moderate load (or even a single 50MB file), the system rapidly exhausted heap space and threw `OutOfMemoryException`s. 
**The Fix:** We had to abandon pure in-memory streaming in favor of a **Disk-Streaming pattern**. The API now immediately flushes incoming files to an `uploads/` directory via `FileStream`, and the `BackgroundTranslationWorker` reads the physical file path, drastically flattening the system memory profile. We also had to introduce a `StartupSweeper` to scrub orphaned files on boot.

---

## 2. Concurrency & The "ALL" Sheets DoS Vulnerability
### ❌ Original Design 
The blueprint assumed a monolithic translation scope—where selecting "ALL" would elegantly process the entire workbook partition-by-partition.

### ✅ Reality (Accidental Denial of Service)
In practice, complex models often contain 40+ densely packed worksheets. Kicking off parallel translation tasks for *every* worksheet simultaneously overwhelmed the application thread pool and saturated the LLM API rate limits, causing an accidental Denial of Service (DoS).
**The Fix:** We had to introduce the **Target Sheet Strategy**. We built a pre-flight `/api/session/inspect` endpoint to dynamically read worksheet names *before* job creation. The UI was augmented to force users to selectively pick a specific target sheet, constraining execution to manageable boundaries.

---

## 3. Extracting VBA Binary Streams
### ❌ Original Design 
Phase III-C mandated using `DocumentFormat.OpenXml` to extract legacy VBA logic from `.xlsm` and `.xlsb` files to avoid expensive COM interop dependencies.

### ✅ Reality (OLE Container Limitations)
`DocumentFormat.OpenXml` is capable of grabbing the compressed `.vbaProject.bin` stream, but it completely lacks native logic to decompress the underlying OLE container format where the actual readable VBA string resides.
**The Fix:** We had to pivot to **EPPlus**, which includes native OLE decompression logic. The `IVbaExtractionEngine` was rewritten to utilize `ExcelPackage`, extracting plain-text VBA for the LLM without spinning up an Excel instance.

---

## 4. LLM Tail Latency & Orchestrator Resilience
### ❌ Original Design 
The spec anticipated transient network drops and mandated `Microsoft.Extensions.Http.Polly` for exponential backoff retries.

### ✅ Reality (The Timeout Trap)
The design underestimated LLM tail latencies. Generative AI calls regularly hang or exceed the default 100-second `HttpClient.Timeout`. Initially, this threw a `TaskCanceledException` that crashed the entire background worker, wiping out hours of partial translation progress.
**The Fix:** We had to introduce explicit `OperationCanceledException` catching directly inside the `ReconciliationOrchestrator`. Mid-partition timeouts are now safely trapped and retried individually, allowing the overall workbook translation to survive localized LLM hangs.

---

## 5. Wasted Compute & Disconnected Sessions
### ❌ Original Design 
Job lifecycle management was implicitly tied to the HTTP request lifecycle or simple in-memory unauthenticated dictionaries.

### ✅ Reality (Ghost Processes)
When users closed their browser tab, the system had no way of knowing. The background worker and LLM continued grinding away, burning thousands of tokens and wasting compute on sessions that no one was watching.
**The Fix:** We had to fundamentally alter the SignalR Hub. `TranslationProgressHub` now binds active `ConnectionId`s to specific `JobId`s. We implemented an `LlmWatchdogService` background worker that enforces a strict 5-minute grace period upon SignalR disconnection before aggressively injecting a cancellation token to abort the LLM call.

---

## 6. Database Persistence: Atomic vs. Incremental Saves
### ❌ Original Design 
Initially, the design implied an atomic save pattern using `dbContext.Database.EnsureCreated()`, saving the entire job only when all sheets/partitions successfully finished.

### ✅ Reality (Fault Tolerance Imperative)
Given the unpredictable nature of external LLM dependencies, atomic saves proved too fragile. A single hallucination on partition 99 would fail the entire transaction, losing 98 successful partitions. 
**The Fix:** We transitioned the schema to a robust **One-to-Many Relational Model** (`TranslationJob` → multiple `TranslationPartitionEntity`). Partitions are securely committed to the SQLite ledger incrementally. Furthermore, we swapped `EnsureCreated()` for formal EF Core Migrations (`Database.Migrate()`) to allow non-destructive schema evolution.

---

## Summary
The original architectural specifications (`enterprise-lifecycle-spec.md` and `architectural-blueprint.md`) provided an excellent mathematical constraint structure (e.g., the variance limits, compression models). However, they failed to account for the **harsh realities of infrastructure**: network unreliability, large file memory constraints, asynchronous ghost processes, and OLE format quirks. The implemented system is significantly more resilient, fault-tolerant, and infrastructure-aware than the original theoretical blueprints.
