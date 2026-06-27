# 04 LLM Domain Bridge

The Domain Interrogation Bridge orchestrates the API interaction between the .NET backend and the Generative AI (Codestral via OpenRouter).

## 1. Core Determinism
LLMs naturally lean toward creative variation. Actuarial execution relies on absolute rigor.
- The API is strictly constrained via `Temperature = 0.0`.
- Outputs must be generated synchronously under deterministic boundaries to ensure the identical prompt always yields the identical AST interpretation.

## 2. System Prompts & Delimiters
The system prompt is housed outside the code layer (`system-prompt.txt`) and ingested dynamically. It mandates a rigorous response schema:
1. **Markdown Explanation:** A readable actuarial synopsis.
2. **The C# Mirror:** The actual executing logic.
   
The code demands a hard delimiter (`===CSHARP_MIRROR===`). The C# output *must* implement `IActuarialReconciliationUnit` and expose an `ExecuteCalculationRow(Dictionary<string, decimal> inputs)` implementation.

## 3. Handling Contextual Gaps (Dictionary Look-Aheads)
Early in development, the LLM consistently hallucinated Dictionary keys when executing logic requiring look-aheads (`inputs["ColX_Plus_1"]`). 
**The Solution:** The `ReconciliationOrchestrator` implements aggressive contextual bridging. By parsing the exact column bounds identified in the Vector Chunking phase, it actively seeds the execution dictionary (`runningState`) with both historical and future values surrounding the target cell *before* Roslyn compilation. This completely eradicated `KeyNotFoundException` run-time crashes.

## 4. Resilience and Timeout Trapping
Given external LLM instability, the 100-second HttpClient limits often triggered `TaskCanceledException`. 
- **Polly Integration:** The HTTP layer is shielded with `WaitAndRetryAsync`.
- **Operation Fallbacks:** Within `ReconciliationOrchestrator.cs`, explicit `OperationCanceledException` catching ensures that if a timeout occurs mid-partition, the orchestrator retries *that specific block* instead of killing the entire Background Job queue.
