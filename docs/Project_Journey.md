# Project Journey – ActuarialXL Translation Engine

## 1. Overview
The **ActuarialXL Translation Engine** is a pipeline that reads actuarial Excel workbooks, extracts data, compresses it into vector payloads, and sends the payloads to a large‑language‑model (LLM) bridge for code generation. The project consists of three major components:
- **ExtractionEngine** – parses worksheets and builds a `RawWorkbookMap`.
- **VectorCompressionEngine** – converts the extracted map into a compact JSON payload.
- **DomainInterrogationBridge** – either a **Mock** bridge (used for unit tests) or a **Live** bridge that talks to OpenRouter via HTTP.

The journey so far spans multiple development phases, each documented in dedicated markdown files.

---

## 2. Phase 1 – Testing and Rationale
This phase is captured in [Phase1_Testing_and_Rationale.md](file:///C:/Github/ActuarialXLpoc/docs/Phase1_Testing_and_Rationale.md).  It documents:
- The original design assumptions for the extraction heuristics.
- Rationale for choosing a deterministic mock bridge to enable fast unit testing.
- Early validation of header‑row detection on a small subset of worksheets.
- Key lessons learned about handling Excel formula cells and native error values.

---

## 3. Phase 2 – Testing and Results
See [Phase2.4_Testing_and_Results.md](file:///C:/Github/ActuarialXLpoc/docs/Phase2.4_Testing_and_Results.md) for a full record of the test suite execution after integrating the live bridge.  Highlights include:
- Unit‑test coverage of 100 % (20/20 tests passing).
- Identification of a header‑row detection failure on *Exercises 18.4 and 18.5*, which prompted the subsequent refactor.
- Performance timings for the end‑to‑end run (≈11 minutes on the sample workbook).

---

## 4. Phase 3 A – Live Bridge Walkthrough
Phase 3 A introduced the **LiveDomainInterrogationBridge** (see the implementation under `Engine/LlmBridge`).  The walkthrough is summarized in the existing sections of this journey (Sections 3‑5) and is also reflected in the spec file:
- [spec‑phase‑3b‑live‑bridge/SPEC.md](file:///C:/Github/ActuarialXLpoc/_bmad-output/specs/spec-phase-3b-live-bridge/SPEC.md) – outlines the technical requirements for the live bridge.
- The DI registration, configuration handling, and HTTP client setup were added in `Program.cs`.
- The header‑detection refactor (expanded to 500 rows) resolves the earlier extraction exception.

---

## 5. Phase 3 B – Risk Analysis & Specification
Risk considerations for the live bridge are documented in [Phase3B_Risk_Analysis.md](file:///C:/Github/ActuarialXLpoc/docs/Phase3B_Risk_Analysis.md).  Key points include:
- Potential latency and rate‑limit impacts when calling OpenRouter.
- Failure‑mode handling for transient network errors (currently a simple retry count; future work will introduce Polly policies).
- Security review of the API key handling (environment variable vs. configuration file).
- **Prompt Governance Implementation:** Enforced Phase III-B intent by updating `Program.cs` to dynamically load the LLM system prompt from `docs/governance/master-prompt-engineering-log.md` during DI initialization instead of defaulting to an empty string. This ensures the Live Bridge sends the rigorously tested prompt persona constraints to OpenRouter, maintaining architectural and semantic integrity.

The accompanying spec and decision‑log files provide the concrete implementation guidance:
- [spec‑phase‑3b‑live‑bridge/SPEC.md](file:///C:/Github/ActuarialXLpoc/_bmad-output/specs/spec-phase-3b-live-bridge/SPEC.md)
- [.decision‑log.md](file:///C:/Github/ActuarialXLpoc/_bmad-output/specs/spec-phase-3b-live-bridge/.decision-log.md)

---

## 6. Build & Test Results
- `dotnet build` succeeds with only warnings about `System.Drawing.Common` (outside the scope of this sprint).
- All unit tests (`dotnet test`) pass (20/20).
- The **end‑to‑end** CLI run now processes *every sheet* and writes compressed payload JSON files under `output/`.

---

## 7. End‑to‑End Run Summary (latest execution)
```
 dotnet run --project ActuarialTranslationEngine.CLI.csproj \
   -- --file edu-2012-c13-01.xlsx --output output --verbose
```
The CLI parsed the workbook, compressed each sheet, and successfully wrote payloads for:
- Tables 13.3, 13.10,
- Examples 14.1, 16.1, 16.3‑Part 1/2, 18.1‑18.12,
- All solution sheets (e.g., *Solution to Exercise 18.5*),
- The previously problematic *Exercises 18.4 and 18.5*.
No extraction exceptions were thrown.

---

## 8. Lessons Learned & Developmental Reflections
- Incremental debugging proved effective: we isolated DI, header detection, and HTTP issues without sweeping changes.
- Clear separation of mock vs. live bridge keeps testing fast while enabling real‑world integration.
- Robust logging (per‑sheet compression messages) was essential to pinpoint failures.

---

## 9. Open Questions (for the next sprint)
- Should the bridge support **streaming** payloads for extremely large sheets, or is the current batch approach sufficient?
- Do we need a more sophisticated **retry policy** (exponential back‑off, circuit‑breaker) beyond the basic `MaxRetries` field?
- Would a **schema validation** step on the extracted map help catch malformed worksheets earlier?

---

## 10. Next Steps
1. Implement a JSON‑based configuration file and load it in `Program.cs`.
2. Refine header‑detection heuristics and add unit tests for edge‑case sheets.
3. Harden HTTP communication with Polly policies.
4. Update the project documentation (README, API docs) with the new architecture diagram.
5. Address the `System.Drawing.Common` vulnerability and perform a full security audit.

---

## 11. Phase 4 - 6 Completion & Enterprise Stabilization
Subsequent to the core translation engine's development, the project evolved into a fully-fledged enterprise architecture:
1. **Phase 4 (Persistence):** Transitioned from ephemeral runs to a persistent `SQLite` data store managed by **EF Core Migrations**. All translations are now durably saved for auditability.
2. **Phase 5 (Enterprise API):** Exposed the engine via an ASP.NET Core Web API wrapper to manage concurrency, HTTP form uploads, and background LLM processing via a robust `BackgroundTranslationWorker`.
3. **Phase 6 (Blazor Web UI):** Replaced the local CLI tool with a rich interactive Governance Dashboard (Blazor). Real-time progress updates are pushed via SignalR.
4. **Validation:** A rigorous Playwright End-to-End (E2E) testing suite was added to validate the entire lifecycle, successfully catching and fixing HTTP upload boundaries and concurrent database locking bugs.

---

*Prepared by Antigravity – your developmental editor.*
