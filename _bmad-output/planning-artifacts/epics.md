---
stepsCompleted: [1, 2, 3]
inputDocuments: ["docs/enterprise-lifecycle-spec.md", "docs/architectural-blueprint.md"]
---

# ActuarialXLpoc - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for ActuarialXLpoc, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Extract pristine formula strings and evaluated values using `ClosedXML`.
FR2: Translate abstract mathematical expression strings into conceptually correct actuarial descriptions via LLM.
FR3: Establish formal, production-grade decoupled layers.
FR4: Implement automated structural serialization and vertical column-collapsing logic.
FR5: Implement continuous change-point algorithms for compression.
FR6: Dynamically load compiled byte arrays into runtime assemblies via `Roslyn`.
FR7: Integrate live endpoint that reads and compiles the markdown-based prompt template.
FR8: Read raw text code of `.vbaProject.bin` using `DocumentFormat.OpenXml`.
FR9: Translate VBA logic into C# methods using an LLM code-compiler.
FR10: Expose parsing, compression, and reconciliation core as an ASP.NET Core WebAPI.

### NonFunctional Requirements

NFR1: Strictly deterministic execution in Phase II (no live network API calls).
NFR2: High-throughput, multi-tenant enterprise microservice framework capable of 50MB+ file processing.
NFR3: Concurrency Gates utilizing asynchronous task offloading via channels or thread-throttled queues.
NFR4: Collectible Memory Sandboxing using `AssemblyLoadContext.Unload()`.
NFR5: Penny-perfect variance evaluation criteria (Delta $\le 0.00001m$).
NFR6: Failure Recovery tracking and logging.

### Additional Requirements

- Implementation restricted strictly to `.NET 8/9 C#` (Python prohibited for core engine).
- Persistence & Storage layer must use `Entity Framework Core` backing into a local embedded `SQLite` database.
- Data payloads must be stored as `JSONB`.

### UX Design Requirements

None.

### FR Coverage Map


FR10: Epic 4 - Expose parsing, compression, and reconciliation core as an ASP.NET Core WebAPI.

## Epic List


### Epic 4: Enterprise Production API & Persistence
**Goal:** External enterprise systems can securely submit massive models to a high-throughput WebAPI. The resulting translated logic is permanently saved to an embedded SQLite database for instant hot-reloading and audibility, avoiding expensive LLM re-computations.
**FRs covered:** FR10




## Epic 4: Headless API & Local Persistence
Expose the translation engine via an ASP.NET Core WebAPI and persist the compiled logic locally using Entity Framework Core and SQLite, establishing the architectural boundaries for future enterprise scaling without over-engineering it today.

### Story 4.1: Persistent Actuarial Audit Trail (SQLite)
As an Actuarial Auditor,
I want the compiled model logic to be permanently saved to a secure embedded database upon successful translation,
So that I can instantly hot-reload and audit the mathematical rules offline without needing to re-trigger expensive LLM computations.

**Acceptance Criteria:**
**Given** a successfully translated and compiled actuarial model,
**When** the PersistenceManager commits the transaction,
**Then** the JSONB payload and model metadata must be saved to a local SQLite .db file using EF Core,
**And** the data layer must be cleanly abstracted so the underlying provider can be swapped in future phases.

**Given** the SQLite database is currently locked by a concurrent transaction,
**When** the PersistenceManager attempts to commit,
**Then** it must implement a retry policy or safely bubble up a concurrency exception without tearing down the host process.

### Story 4.2: ASP.NET Core WebAPI Wrapper
As a consuming system,
I want to interact with the engine via RESTful HTTP endpoints,
So that I can submit Excel files and retrieve evaluated logic without needing to reference the internal engine DLLs directly.

**Acceptance Criteria:**
**Given** the running ASP.NET Core WebAPI,
**When** a client POSTs an actuarial .xlsx or .xlsm file to the ingestion endpoint,
**Then** the API must route the file through the extraction and translation pipeline,
**And** return a 200 OK containing the serialized evaluation results,
**And** properly isolate the memory by invoking AssemblyLoadContext.Unload() upon request completion to prevent memory leaks.

**Given** the running ASP.NET Core WebAPI,
**When** a client POSTs a malformed payload or an unsupported file type,
**Then** the API must immediately return a 400 Bad Request with a clear validation error,
**And** still properly invoke AssemblyLoadContext.Unload() to ensure no orphan memory partitions remain.

