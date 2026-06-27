# 06 Persistence and Lifecycle Management

A production application managing multi-minute LLM jobs requires robust background persistence and active ghost-process management.

## 1. Relational Fault Tolerance (Incremental Saves)
The initial architecture proposed a monolithic atomic database commit at the conclusion of an entire job. However, if a timeout occurred on partition 45 out of 50, all previous 44 partitions were permanently lost.
- **The Solution:** We migrated the `SqlitePersistenceManager` to a **One-to-Many Schema**.
- A root `TranslationJobEntity` (tracked via an immutable `Guid`) acts as the header. As each vector chunk finishes the LLM/Roslyn loop, a `TranslationPartitionEntity` is immediately appended via `SavePartitionAsync`. Even upon a catastrophic fault, the user retains all prior successfully certified C# conversions.

## 2. Async Background Job Pattern
The API cannot hold an HTTP connection open for 15 minutes waiting for an entire workbook to finish processing.
- When an upload finishes, the `SessionEndpoints` return a `202 Accepted` and a tracker `JobId`.
- The job request is serialized and placed inside the thread-safe `ITranslationJobQueue` (`System.Threading.Channels`).
- The `BackgroundTranslationWorker` acts as an `IHostedService`, pulling operations sequentially and isolating the API ThreadPool.

## 3. Orphan Process Management (Watchdogs & Sweepers)
If a user closes their browser window while a long-running LLM job is pending, the Engine traditionally continues grinding—costing API tokens and compute power. We implemented two distinct mitigation strategies:
- **LlmWatchdogService:** Binds the physical client `ConnectionId` via SignalR to the `JobId`. If a client disconnects, a 5-minute grace period initiates. If they do not reconnect, the Watchdog injects a `CancellationToken`, immediately halting the `ReconciliationOrchestrator` execution.
- **StartupSweeper:** Scans the SQLite database upon API boot. Any jobs stuck in `Pending` or `Running` states from a previous physical server crash are force-marked as `Failed`, and abandoned physical uploads mapping to those jobs are expunged from the disk.
