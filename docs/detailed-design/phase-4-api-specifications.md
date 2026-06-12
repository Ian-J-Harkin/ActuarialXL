# Phase IV: Enterprise API Detailed Specifications & Assumptions

This document acts as an expansion of the `enterprise-lifecycle-spec.md` (Phase IV) to capture the explicit routing stubs, API architectural boundaries, and performance assumptions required to host the `ActuarialTranslationEngine`.

## 1. Hosting Architecture
- **Framework:** ASP.NET Core 10 WebAPI
- **Deployment Strategy:** Stateless Container (Docker/Kubernetes ready)
- **Dependency Injection (DI):** 
  - `RoslynReconciliationEngine` must be registered as a **Transient** or **Scoped** service, never Singleton, to ensure `AssemblyLoadContext` is garbage collected between web requests.
  - `ActuarialExtractionEngine` (ClosedXML) can be **Scoped**.

## 2. API Routing Stubs

### A. Spreadsheet Ingestion Endpoint
**POST** `/api/v1/workbooks/analyze`
- **Payload:** `multipart/form-data` (File upload: `.xlsx`)
- **Action:** Streams the Excel file directly into `ClosedXML` without saving to physical disk to prevent I/O blocking.
- **Returns:** `202 Accepted` with an `AnalysisJobId` for asynchronous polling (or `200 OK` if processing is fast enough to remain synchronous).

### B. Archetype Extraction Endpoint
**GET** `/api/v1/workbooks/{jobId}/archetypes`
- **Action:** Retrieves the list of identified Actuarial Archetypes (e.g., "Universal Life", "Variable Annuity") extracted from the workbook.

### C. Logic Graph Export Endpoint
**GET** `/api/v1/workbooks/{jobId}/logic-graph`
- **Action:** Returns the fully compiled `CSharpCompilation` syntax tree mapping, detailing every cell's mathematical formula translated into standard C# actuarial logic.
- **Returns:** JSON representation of the `IVectorCompressionEngine` output.

## 3. Concurrency & Performance Assumptions
- **Throttling/Rate Limiting:** Roslyn compilation is CPU-heavy. The API must implement `AspNetCore.RateLimiting` to prevent CPU exhaustion from concurrent compilation requests.
- **Memory Boundaries:** Because Phase III-B dynamically compiles code at runtime using Roslyn, there is a risk of memory leaks if assemblies are not unloaded. The API assumes strict adherence to `Collectible AssemblyLoadContext` to drop dynamic DLLs from memory after the HTTP request terminates.

## 4. LLM Bridge Configuration
- **Live Pass Constraint:** The API will use standard `HttpClientFactory` patterns for the LLM live pass. It assumes the LLM endpoint (Mistral/Codestral) is securely routed via a backend API Gateway, and no API keys will be hardcoded. Environment variables (`ACTUARIAL_LLM_API_KEY`) will be managed by enterprise secret managers (e.g., Azure KeyVault / AWS Secrets Manager).
