---
id: SPEC-phase-3b-live-bridge
companions: 
  - architecture-diagrams.md
  - prompt-template.md
  - error-recovery-matrix.md
sources: 
  - ../../../docs/Phase3B_Implementation_Plan.md
  - ../../../docs/detailed-design/phase-3b-live-bridge-and-reconciliation.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Phase III-B: Live Bridge & Reconciliation

## Why

We must transition the Actuarial Semantic Translation Engine from a simulated environment to production-grade live execution. This phase closes the gap between structurally extracting actuarial topologies (Phase II) and executing LLM-generated translation code securely at runtime. By integrating the `mistralai/codestral-2508` model and dynamically compiling the result with an isolated Roslyn engine, we establish a verifiable, mathematically proven pipeline that prevents silent calculation failures.

## Capabilities

- id: CAP-1
  intent: The Engine can transmit compressed vector blocks to an external LLM using a strict prompt template to generate C# logic.
  success: Network calls return an OpenAI-compatible payload split by the `===CSHARP_MIRROR===` delimiter.

- id: CAP-2
  intent: The Engine can dynamically parse and compile the LLM-generated C# string in-memory.
  success: Roslyn generates a runnable assembly implementing `IActuarialReconciliationUnit` using `isCollectible: true`.

- id: CAP-3
  intent: The Engine enforces strict security scanning on the Abstract Syntax Tree (AST) before compilation.
  success: Any code attempting file access, network calls, infinite loops, or unauthorized namespace imports throws an `ActuarialDynamicCompilationException` and halts immediately.

- id: CAP-4
  intent: The Engine mathematically verifies the compiled C# logic against three representative rows (First, Mid, Last) per partition.
  success: Variance is $\le 0.00001$ against the Excel target row; mismatches throw an `ActuarialLogicLeakException`.

- id: CAP-5
  intent: The Engine orchestrates automated error recovery, re-prompting the LLM with diagnostic traces upon compilation failures.
  success: The pipeline catches compilation exceptions, submits the error string to the LLM for correction, and successfully compiles the revised code up to a limit of 2 retries.

- id: CAP-6
  intent: The Engine intelligently selects the validation target column for wide balancing tables.
  success: For Archetype C structures, the target is automatically bound to the column whose header contains "Total", "Net", "Reserve", or "Balance".

## Constraints

- Code execution MUST occur under a 5-second timeout via `CancellationToken`.
- Target LLM temperature MUST be `0.0`.
- The Live Bridge MUST retry up to `MaxRetries` using exponential backoff for network timeouts and HTTP 429 errors.
- Structural Error Suppression MUST force the row selector to skip any row containing `DisruptiveNodes`.
- The AST Safety Scanner MUST be fully unit-tested as a Gate Condition prior to running any live LLM queries.

## Non-goals

- Deploying to Azure or configuring CI/CD pipelines (deferred to Phase IV).
- Supporting UI components or web frontends.
- Processing legacy VBA code dynamically (VBA handling concluded in Phase II-B).

## Success signal

The CLI can point to `edu-2012-c13-01.xlsx`, automatically request and compile the LLM specification via OpenRouter, and successfully run the 3-row validation loop against Table 13.4 and Example 18.4 without throwing security or logic exceptions.
