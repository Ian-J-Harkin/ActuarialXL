# Story 5.2: LLM Imperative Code-to-Code Translation

**Status:** review

## Story Foundation
As an Actuary,
I want the LLM to translate procedural VBA loops into deterministic C# execution rules,
So that imperative logic (like Monte Carlo simulations) can be evaluated by the Roslyn engine.

### Acceptance Criteria:
**Given** the raw VBA text extracted from the workbook,
**When** the IDomainInterrogationBridge processes the payload with the VBA flag enabled,
**Then** the LLM must return stateless, pure C# code implementing `IActuarialReconciliationUnit`,
**And** the Roslyn engine must successfully compile and execute it with a variance Delta $\le 0.00001m$.

## Dev Agent Guardrails & Technical Context
### Architecture Compliance
- Enhance `IDomainInterrogationBridge` to process `VbaModuleCode`.
- Ensure `LiveDomainInterrogationBridge` utilizes a `VbaSystemPrompt`.
- Ensure `MockDomainInterrogationBridge` returns a valid dynamic unit.
