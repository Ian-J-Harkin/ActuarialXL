# Actuarial Semantic Translation Engine: Project Status Tracker

This document tracks the execution and completion status of the phases defined in the [Enterprise Lifecycle Specification](enterprise-lifecycle-spec.md) and the [Architectural Blueprint](../architectural-blueprint.md).

## Phase I: The Environment & Ground Truth Spike
**Status:** ✅ **COMPLETED**
* **Goal:** Establish the exact empirical state of `edu-2012-c13-01.xlsx` and define the architectural constraints without relying on assumptions.
* **Key Deliverable:** [Phase 1 Testing & Rationale](Phase1_Testing_and_Rationale.md)

---

## Phase II: The Structural Extraction Layer (.NET)
**Status:** 🔄 **IN PROGRESS**
* **Goal:** Build the deterministic `.NET` parser to extract spatial mappings and compress recursive mathematical formulas into standard JSON payloads.
* **Key Deliverables:** 
  * `ActuarialExtractionEngine` (Retrieves headers and formulas)
  * `VectorCompressionEngine` (Collapses vertical data blocks)

### Execution Steps
- [x] **Step II.1: Domain Scaffolding & Interfaces**
  - Define `RawWorkbookMap`, `CompressedVectorBlock`, and `IActuarialExtractionEngine`.
- [x] **Step II.2: Extraction Engine (ClosedXML)**
  - Implement real Excel parsing logic.
  - Handled snags logged in [Phase 2 Testing & Rationale](Phase2_Testing_and_Rationale.md) (e.g., Row 6 start, 14 columns).
- [x] **Step II.3: Compression Engine (Vector Chunking)**
  - Implement time-series collapse logic to reduce 60+ rows down to 2-3 logical partitions.

---

## Phase II-B: VBA Code-to-Code Ingestion Pipeline
**Status:** ⏳ **PENDING**
* **Goal:** Implement the raw text stream extraction of `.vbaProject.bin` using `DocumentFormat.OpenXml` to bypass `ClosedXML` limitations.
* **Reference:** Added via design addendum to the [Enterprise Lifecycle Specification](enterprise-lifecycle-spec.md#phase-ii-b-vba-code-to-code-ingestion-pipeline-addendum).

---

## Phase III-A: The CLI Orchestrator (Multi-Archetype Expansion)
**Status:** ⏳ **PENDING**
* **Goal:** Expand from parsing a single sheet (`Table 13.4`) to orchestrating a full CLI that dynamically routes all 4 archetypes (Time-Series, Stochastic, Balancing Ledgers, Variable Adjusters).
* **Reference:** [Phase III-A Detailed Design](detailed-design/phase-3a-archetype-definitions.md)

---

## Phase III-B: Live Bridge & Reconciliation Validation
**Status:** ⏳ **PENDING**
* **Goal:** Orchestrate the LLM to write stateless C# strings, execute them dynamically via an in-memory Roslyn compiler, and test the outputs against the raw spreadsheet data (Penny-Perfect Balance).
* **Reference:** [Phase III-B Detailed Design](detailed-design/phase-3b-live-bridge-and-reconciliation.md)

---

## Phase IV: Persistence & Storage Layer (Database Metadata Pattern)
**Status:** ⏳ **PENDING**
* **Goal:** Implement the embedded `SQLite` database using `Entity Framework Core` to store the versioned C# code and Auditable Markdown payloads in `JSONB` format for hot-reloading.
* **Reference:** [Architectural Blueprint: Section 8](../architectural-blueprint.md#8-persistence--storage-layer-the-hybrid-database-metadata-pattern)
