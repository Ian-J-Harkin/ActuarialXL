# 02 Ingestion and Extraction Layer

The ingestion process solves structural vulnerabilities surrounding memory constraints, DoS attacks, and binary object isolation.

## 1. Disk-Streaming Ingestion Strategy (Anti-OOM)
Given that actuarial models easily exceed 50MB and host complex dependency trees, native in-memory parsing is blocked by design to prevent `OutOfMemoryException` crashes.
- When an upload arrives via Minimal API endpoints (`/api/session/create`), the multipart form data is routed directly to a physical `FileStream`. 
- The payload is hashed, saved to `uploads/{FileHash}.xlsx`, and mapped to the database. The `BackgroundTranslationWorker` interacts solely with this physical file path rather than byte arrays.

## 2. The Target Sheet Strategy
During load testing, allowing the engine to process an entire workbook (e.g., passing "ALL" sheets) simultaneously resulted in massive parallel explosions—initiating 40+ concurrent jobs and immediately saturating LLM API rate limits.
- **Pre-flight Inspection:** The engine enforces a `/api/session/inspect` endpoint that extracts the sheet names *before* job enqueueing.
- **Selective Scope:** Users must explicitly target one sheet. This caps the process thread limit and memory overhead for heavy enterprise loads.

## 3. Declarative Formula Extraction (ClosedXML)
For Archetypes A (Time-Series), B (Stochastic Formulas), C (Ledgers), and D (Variable Adjusters), the engine routes through `ClosedXML`.
- Extracts standard `.FormulaA1` representations.
- Captures `DisruptiveNodes` (e.g., `#REF!`, `#DIV/0!`, `RAND()`) and wraps them safely, warning the user instead of terminating the process.

## 4. Imperative VBA Logic Extraction (EPPlus)
For Archetype E (Imperative Legacy State Mutation), standard XML parsing falls apart. VBA logic is locked inside an OLE container stream (`.vbaProject.bin`).
- **Tool Swap:** Since `DocumentFormat.OpenXml` cannot natively decompress OLE container limits without massive overhead, the extraction layer delegates to `EPPlus`.
- **Pipeline:** `VbaExtractionEngine.cs` identifies `package.Workbook.VbaProject.Modules` and pulls the raw text stream as a `VbaModuleCode` payload, presenting the pure imperative loop to the LLM for conversion into stateless C#.
