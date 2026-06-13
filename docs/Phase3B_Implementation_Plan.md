# Phase III-B Implementation Plan: Live Bridge & Reconciliation Validation

The goal of Phase III-B is to implement the **Live Bridge & Reconciliation Validation** layer. We will replace the mock LLM bridge with a live `OpenRouter` integration that passes our compressed vectors to an execution-grade LLM (`mistralai/codestral-2508`). The LLM will translate the mathematical topology into a plain-English actuarial specification and generate a stateless C# class representing the calculation. We will then dynamically compile this generated C# code in memory using Roslyn, run **three representative rows** of real spreadsheet data through it, and mathematically verify that the LLM's logic matches the original Excel spreadsheet evaluated results to a penny-perfect variance ($\le 0.00001$).

> [!IMPORTANT]
> **Prerequisite:** Set your OpenRouter API key as a Windows environment variable before execution:
> ```powershell
> [Environment]::SetEnvironmentVariable("ACTUARIAL_LLM_API_KEY", "your_key_here", "User")
> ```

> [!CAUTION]
> **Ratified Decisions (from Risk Analysis review):**
> - **Model:** `mistralai/codestral-2508` — confirmed by user.
> - **Orchestrator:** Dedicated `ReconciliationOrchestrator` in Engine (not inside CLI).
> - **isCollectible:** `true` from the start (Architect Override — not deferred to Phase IV).
> - **Row Sampling:** 3 rows per partition (First, Mid, Last) — not 1.
> - **Cross-Reference:** [Phase3B_Risk_Analysis.md](Phase3B_Risk_Analysis.md), [architectural-blueprint.md §9](../architectural-blueprint.md)

---

## Proposed Changes

Execution is ordered to satisfy the **Gate Condition**: the AST Safety Scanner and Execution Timeout must be built and tested *before* the first live LLM call.

---

### Step 1: Core Exceptions & Configuration (ActuarialTranslationEngine.Core)

#### [MODIFY] [ActuarialTranslationEngine.Core.csproj](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/ActuarialTranslationEngine.Core.csproj)
- Add reference to `Microsoft.CodeAnalysis.Common` for the `Diagnostic` type used in compilation exceptions.

#### [NEW] [ActuarialLlmBridgeException.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Exceptions/ActuarialLlmBridgeException.cs)
- Exception for API failures, timeouts, and missing `===CSHARP_MIRROR===` delimiters.

#### [NEW] [ActuarialDynamicCompilationException.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Exceptions/ActuarialDynamicCompilationException.cs)
- Exception for Roslyn compilation failures, containing an `IEnumerable<Diagnostic>` to pass errors back to the LLM.

#### [NEW] [ActuarialLogicLeakException.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Exceptions/ActuarialLogicLeakException.cs)
- Exception thrown when the variance between the LLM calculation and the spreadsheet result exceeds `0.00001`.

#### [NEW] [LlmBridgeConfiguration.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Models/LlmBridgeConfiguration.cs)
- Configuration class: `EndpointUrl`, `ModelName`, `ApiKey`, `SystemPrompt`, `MaxRetries`, `RetryDelayMs`.

---

### Step 2: Gate Condition — AST Safety Scanner & Execution Timeout (ActuarialTranslationEngine.Engine)

> [!CAUTION]
> **These two components MUST be implemented and unit-tested before any subsequent steps.**

#### [MODIFY] [ActuarialTranslationEngine.Engine.csproj](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/ActuarialTranslationEngine.Engine.csproj)
- Add NuGet package `Microsoft.CodeAnalysis.CSharp` for runtime compilation and syntax tree inspection.

#### [NEW] [AstSafetyScanner.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/Roslyn/AstSafetyScanner.cs)
- Extends `CSharpSyntaxWalker`.
- Rejects `using` directives beyond `System` and `System.Collections.Generic`.
- Rejects references to `System.IO`, `System.Net`, `System.Diagnostics`, `System.Reflection`, `System.Runtime.InteropServices`, `System.Threading`.
- Rejects object instantiation of types outside `Dictionary<,>`, `List<>`, and primitives.
- Exposes `IReadOnlyList<string> Violations` for reporting.

#### [NEW] [RoslynReconciliationEngine.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/Roslyn/RoslynReconciliationEngine.cs)
- Implements `IRoslynReconciliationEngine`.
- Calls `AstSafetyScanner` on the parsed `SyntaxTree` before compilation — throws immediately if violations found.
- Uses `CSharpCompilation` with **restricted metadata references** (no `System.IO.dll`, no `System.Net.Http.dll`).
- Uses `AssemblyLoadContext` with **`isCollectible: true`** inside a `using` scope.
- Wraps `ExecuteCalculationRow()` in `Task.Run()` with a **5-second `CancellationToken`** timeout.
- Compares output to `expectedSpreadsheetResult` and throws `ActuarialLogicLeakException` on failure.

#### Unit Tests (Gate Validation)
- `AstSafetyScannerTests.cs`: Verify scanner rejects `System.IO.File.Delete()`, `Process.Start()`, `while(true)`.
- `AstSafetyScannerTests.cs`: Verify scanner passes clean `IActuarialReconciliationUnit` implementations.
- `RoslynReconciliationEngineTests.cs`: Verify compilation + execution of a known-good C# string returns the expected result.
- `RoslynReconciliationEngineTests.cs`: Verify `ActuarialLogicLeakException` is thrown when variance > 0.00001.
- `RoslynReconciliationEngineTests.cs`: Verify 5-second timeout fires on an infinite loop.

---

### Step 3: Live LLM Bridge (ActuarialTranslationEngine.Engine)

#### [NEW] [LiveDomainInterrogationBridge.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/Bridges/LiveDomainInterrogationBridge.cs)
- Implements `IDomainInterrogationBridge`.
- Uses `HttpClient` to POST to OpenRouter (`https://openrouter.ai/api/v1/chat/completions`).
- Sets `temperature = 0.0` for deterministic output.
- Parses the OpenAI-compatible response envelope (`choices[0].message.content`).
- Splits response on `===CSHARP_MIRROR===` delimiter into `TranslationOutput`.
- Wraps extracted C# class body with required `using` statements.
- Retry logic: exponential backoff for `TaskCanceledException` and HTTP 429.
- Retry logic: re-prompt with reminder if delimiter is missing (max 1 retry).

#### Unit Tests
- `LiveDomainInterrogationBridgeTests.cs`: Mock `HttpMessageHandler` returning a well-formed response — verify correct splitting.
- `LiveDomainInterrogationBridgeTests.cs`: Mock returning response without delimiter — verify `ActuarialLlmBridgeException`.
- `LiveDomainInterrogationBridgeTests.cs`: Mock returning HTTP 429 — verify retry behaviour.

---

### Step 4: Reconciliation Orchestrator (ActuarialTranslationEngine.Engine)

#### [NEW] [ReconciliationOrchestrator.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/ReconciliationOrchestrator.cs)
- Coordinates the full pipeline: Bridge → AST Scan → Compile → Execute → Verify.
- Implements **Compilation Error Re-Prompting** loop (max 2 retries): catches `ActuarialDynamicCompilationException`, sends diagnostics back to the LLM for correction.
- Implements **Multi-Row Sampling** (RISK-A1): selects First Row, Mid-Point Row, Last Row from each partition. Applies Structural Error Suppression (skips rows with `DisruptiveNodes`).
- Implements **Header-Matching Target Selector** for Archetype C: scans column headers for "Total", "Net", "Reserve", "Balance" to identify the validation target column.
- Outputs a `ReconciliationResult` per partition: `Certified`, `LogicLeak`, `CompilationFailure`, or `Inconclusive_Source_Corrupted`.

---

### Step 5: CLI Integration (ActuarialTranslationEngine.CLI)

#### [MODIFY] [Program.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.CLI/Program.cs)
- Replace `MockDomainInterrogationBridge` with `LiveDomainInterrogationBridge` DI registration.
- Register `IRoslynReconciliationEngine`, `ReconciliationOrchestrator`, `LlmBridgeConfiguration`.
- Inject `HttpClient` via `IHttpClientFactory`.
- Read `ACTUARIAL_LLM_API_KEY` from environment variable.
- Load system prompt from `docs/governance/master-prompt-engineering-log.md` at startup.

#### [MODIFY] [CLIOrchestrator.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.CLI/CLIOrchestrator.cs)
- After compressing the topology, pass `CompressedVectorBlock` + `RawWorkbookMap` to the `ReconciliationOrchestrator`.
- Log per-partition results: `✅ CERTIFIED`, `❌ LOGIC LEAK`, `⚠️ COMPILATION FAILURE`, `⏭️ INCONCLUSIVE`.
- Write verified LLM Markdown specs and C# code to the output directory.

---

## Verification Plan

### Automated Tests (Gate Condition — must pass before live calls)
- `dotnet test` — all existing extraction and compression tests remain green.
- `AstSafetyScannerTests` — scanner correctly rejects dangerous code patterns.
- `RoslynReconciliationEngineTests` — compilation, execution, variance checking, and timeout all work.

### Automated Tests (Post-Integration)
- `LiveDomainInterrogationBridgeTests` — mocked HTTP response parsing, delimiter splitting, retry logic.

### Manual Verification (End-to-End)
- Run CLI against `edu-2012-c13-01.xlsx` targeting `Table 13.4` (Archetype A — simplest case).
- Verify the system hits OpenRouter, retrieves the translation, compiles the C#, and passes the 3-row variance check.
- Inspect the generated Markdown specification for actuarial correctness.
- Run against `Example 18.4` (Archetype C) to validate the Header-Matching Target Selector.
