# Milestones – Pick‑up Points after Phase III‑B

The following markers are intended to let us resume work cleanly at a later time.  Each item is a concrete, self‑contained goal that can be tackled independently.

---

## M1 – Verify End‑to‑End Output
- Open the `output/` folder and confirm that every expected sheet has a `*_compressed.json` file.
- Run a quick sanity‑check script (or manual inspection) to ensure the JSON schema matches `RawWorkbookMap`.
- Record any missing or malformed payloads in `docs/output‑validation.md`.

## M2 – Harden Header Detection
- Review the current header‑row search (500 rows) and decide if a more data‑driven heuristic is needed.
- Add unit tests for edge‑case worksheets (headers deeper than 500 rows, formula‑only headers).
- Document the chosen strategy in `docs/header‑heuristics.md`.

## M3 – Add Retry/Polly Policy for HTTP Calls
- Introduce a `Polly` policy (exponential back‑off, circuit‑breaker) around the `HttpClient` used by `LiveDomainInterrogationBridge`.
- Make the policy configurable via `LlmBridgeConfiguration`.
- Verify behaviour with a mock that forces transient failures.

## M4 – Centralise Configuration
- Create a JSON settings file (e.g., `appsettings.json`) that holds `OpenRouterApiKey`, endpoint, model, and retry options.
- Update `Program.cs` to load this file and bind it to `LlmBridgeConfiguration`.
- Add documentation of the config schema in `docs/configuration‑guide.md`.

## M5 – Expand Documentation & API Reference
- Generate XML comments for public classes (`ActuarialExtractionEngine`, `LiveDomainInterrogationBridge`).
- Use `docfx` (or similar) to produce an HTML API reference.
- Add a high‑level architecture diagram (update `architecture‑diagrams.md`).

## M6 – Security & Dependency Review
- Address the `System.Drawing.Common` warning (upgrade or isolate usage).
- Run `dotnet list package --vulnerable` and resolve any other reported issues.
- Record the remediation steps in `docs/security‑audit.md`.

---

> **Commitment to clearer communication** – Going forward I will **always ask for explicit permission** before running commands, building, or making any code changes.  I will also provide a concise status summary *before* proceeding with any action, so you can approve or adjust the plan.

*Prepared on 2026‑06‑14 for the ActuarialXL project.*
