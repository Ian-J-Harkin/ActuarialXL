# Goal Description

The goal of Phase III-B is to implement the **Live Bridge & Reconciliation Validation** layer. We will replace the mock LLM bridge with a live `OpenRouter` integration that passes our compressed vectors to an execution-grade LLM (e.g., Codestral). The LLM will translate the mathematical topology into a plain-English actuarial specification and generate a stateless C# class representing the calculation. We will then dynamically compile this generated C# code in memory using Roslyn, run a representative row of real spreadsheet data through it, and mathematically verify that the LLM's logic matches the original Excel spreadsheet evaluated result to a penny-perfect variance ($\le 0.00001$).

## User Review Required

> [!IMPORTANT]  
> **API Key Injection:** To run the CLI with the Live Bridge, you will need to set an environment variable named `ACTUARIAL_LLM_API_KEY` containing your OpenRouter API key.
> **Model Selection:** The spec defaults to `mistralai/codestral-2508`. Please confirm this model is still correct and active in OpenRouter for our tests.

## Open Questions

> [!WARNING]  
> **Reconciliation Orchestrator Location:** The detailed design mentions a repair loop when the Roslyn compilation fails (passing the compiler diagnostics back to the LLM). Should this repair loop orchestration live inside the `CLIOrchestrator`, or should we introduce a dedicated `ReconciliationOrchestrator` service in the Engine to handle the back-and-forth between the Bridge and the Roslyn compiler? (I propose adding a dedicated `ReconciliationOrchestrator` to keep the CLI clean).

## Proposed Changes

---

### ActuarialTranslationEngine.Core

Add the new exception types required for the Live Bridge and Roslyn compilation.

#### [MODIFY] [ActuarialTranslationEngine.Core.csproj](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/ActuarialTranslationEngine.Core.csproj)
- Add reference to `Microsoft.CodeAnalysis.Common` for the `Diagnostic` type used in compilation exceptions.

#### [NEW] [ActuarialLlmBridgeException.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Exceptions/ActuarialLlmBridgeException.cs)
- Exception for API failures, timeouts, and missing delimiters.

#### [NEW] [ActuarialDynamicCompilationException.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Exceptions/ActuarialDynamicCompilationException.cs)
- Exception for Roslyn compilation failures, containing an `IEnumerable<Diagnostic>` to pass errors back to the LLM.

#### [NEW] [ActuarialLogicLeakException.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Exceptions/ActuarialLogicLeakException.cs)
- Exception thrown when the variance between the LLM calculation and the spreadsheet result exceeds `0.00001`.

#### [NEW] [LlmBridgeConfiguration.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Core/Models/LlmBridgeConfiguration.cs)
- Configuration class containing `EndpointUrl`, `ModelName`, `ApiKey`, `SystemPrompt`, `MaxRetries`, etc.

---

### ActuarialTranslationEngine.Engine

Implement the live OpenRouter bridge and the Roslyn compilation engine.

#### [MODIFY] [ActuarialTranslationEngine.Engine.csproj](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/ActuarialTranslationEngine.Engine.csproj)
- Add NuGet package `Microsoft.CodeAnalysis.CSharp` for runtime compilation.

#### [NEW] [LiveDomainInterrogationBridge.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/Bridges/LiveDomainInterrogationBridge.cs)
- Implements `IDomainInterrogationBridge`.
- Uses `HttpClient` to call OpenRouter API.
- Implements response parsing, splitting on `===CSHARP_MIRROR===`, and wraps the resulting C# code in standard `using` directives and namespace.
- Implements retry logic for `TaskCanceledException` and HTTP 429 Too Many Requests.

#### [NEW] [RoslynReconciliationEngine.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/Roslyn/RoslynReconciliationEngine.cs)
- Implements `IRoslynReconciliationEngine`.
- Uses `CSharpCompilation` to emit an in-memory assembly.
- Uses `AssemblyLoadContext` to load and execute the `IActuarialReconciliationUnit`.
- Compares output to `expectedSpreadsheetResult` and throws `ActuarialLogicLeakException` on failure.

#### [NEW] [ReconciliationOrchestrator.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.Engine/ReconciliationOrchestrator.cs)
- Coordinates the LLM bridge and Roslyn engine.
- Implements the "Compilation Error Re-Prompting" loop (max 2 retries) if `ActuarialDynamicCompilationException` is thrown.
- Selects the target validation row (avoiding `DisruptiveNodes`) and the target validation column (using Header-Matching heuristic for "Total", "Net", "Reserve", "Balance", or defaulting to the rightmost column).

---

### ActuarialTranslationEngine.CLI

Wire up the Live Bridge into the CLI.

#### [MODIFY] [Program.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.CLI/Program.cs)
- Replace `MockDomainInterrogationBridge` with `LiveDomainInterrogationBridge`.
- Register `IRoslynReconciliationEngine` and `ReconciliationOrchestrator`.
- Inject `HttpClient` and read the `ACTUARIAL_LLM_API_KEY` environment variable.

#### [MODIFY] [CLIOrchestrator.cs](file:///C:/Github/ActuarialXLpoc/ActuarialTranslationEngine.CLI/CLIOrchestrator.cs)
- After compressing the topology, pass the `CompressedVectorBlock` and `RawWorkbookMap` to the `ReconciliationOrchestrator`.
- Log the verified LLM output to disk or output a failure state if validation fails.

---

## Verification Plan

### Automated Tests
- `dotnet test` to ensure existing `ActuarialExtractionEngine` and `VectorCompressionEngine` tests still pass.
- Write new unit tests for `LiveDomainInterrogationBridge` (using a mocked `HttpMessageHandler`) to verify response splitting and error handling.
- Write new unit tests for `RoslynReconciliationEngine` to verify it throws `ActuarialLogicLeakException` correctly when the variance is > 0.00001, and passes when it is $\le 0.00001$.

### Manual Verification
- Run the CLI against `edu-2012-c13-01.xlsx` (e.g., `Table 13.4`).
- Validate that the system successfully hits OpenRouter, retrieves the translation, successfully compiles the C#, and reports a "Mathematically Certified" status.
