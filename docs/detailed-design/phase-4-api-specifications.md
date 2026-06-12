# Phase IV: Enterprise API Detailed Specifications & Assumptions

This document acts as an expansion of the `enterprise-lifecycle-spec.md` (Phase IV) to capture the explicit routing stubs, API architectural boundaries, and performance assumptions required to host the `ActuarialTranslationEngine`.

**Related Documents:**
- For database architecture, entity models, and runtime rule execution, see: [Phase 4 Persistence Schema](phase-4-persistence-schema.md).

## 1. Hosting Architecture
- **Framework:** ASP.NET Core 8/9 WebAPI (Minimal APIs)
- **Deployment Strategy:** Stateless Container (Docker/Kubernetes ready)
- **Dependency Injection (DI):** 
  - `ActuarialGovernanceDbContext` registered as a **Scoped** service.
  - `RoslynReconciliationEngine` must be registered as a **Transient** or **Scoped** service, never Singleton, to ensure `AssemblyLoadContext` is garbage collected between web requests.
  - `ActuarialExtractionEngine` (ClosedXML/OpenXml) can be **Scoped**.

## 2. API Routing Stubs

### A. Spreadsheet Ingestion Endpoint
**POST** `/api/v1/workbooks`
- **Payload:** `multipart/form-data` (File upload: `.xlsx`, `.xlsm`, `.xlsb`)
- **Action:** Streams the Excel file directly into the Extraction Engine without saving to physical disk. Parses logic and initializes an `ActuarialWorkbook` entity with associated `WorksheetTopology` entities.
- **Returns:** `202 Accepted` with `{workbookId}` for asynchronous polling.

### B. Worksheet Topology Endpoint
**GET** `/api/v1/workbooks/{workbookId}/worksheets`
- **Action:** Retrieves the list of identified Actuarial Archetypes extracted from the workbook, mapped to their `WorksheetTopology` Guid records.

### C. Translation Rule Export Endpoint
**GET** `/api/v1/worksheets/{worksheetId}/active-rule`
- **Action:** Returns the fully compiled syntax tree mapping, detailing every cell's mathematical formula translated into standard C# actuarial logic. This retrieves the `TranslationPayload` of the active `RuleTranslationVersion`.
- **Returns:** JSON representation of the `IVectorCompressionEngine` output and active rule payload.

### D. Runtime Execution Endpoint
**POST** `/api/v1/worksheets/{worksheetId}/execute`
- **Payload:** JSON dictionary of row inputs (`Dictionary<string, decimal>`).
- **Action:** Invokes `ExecuteActiveRuleAsync` to dynamically load the active C# rule (`Payload.GeneratedCSharpMirrorCode`) from the database and execute it via the Roslyn compiler.
- **Returns:** The computed `decimal` result.

## 3. Concurrency & Performance Assumptions
- **Throttling/Rate Limiting:** Roslyn compilation is CPU-heavy. The API must implement `AspNetCore.RateLimiting` to prevent CPU exhaustion from concurrent compilation requests.
- **Memory Boundaries:** Because Phase III-B dynamically compiles code at runtime using Roslyn, there is a risk of memory leaks if assemblies are not unloaded. The API assumes strict adherence to `Collectible AssemblyLoadContext` to drop dynamic DLLs from memory after the HTTP request terminates.

## 4. LLM Bridge Configuration
- **Live Pass Constraint:** The API will use standard `HttpClientFactory` patterns for the LLM live pass. It assumes the LLM endpoint (Mistral/Codestral) is securely routed via a backend API Gateway, and no API keys will be hardcoded. Environment variables (`ACTUARIAL_LLM_API_KEY`) will be managed by enterprise secret managers (e.g., Azure KeyVault / AWS Secrets Manager).
