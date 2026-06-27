# 08 QA and Testing Strategy

Because this application bridges deterministic structural logic with non-deterministic LLM generations, the testing layer must be fiercely guarded.

## 1. Headless E2E Integration (Playwright)
To prove the full cycle (File Upload → SignalR Stream → LLM Translation → Database Commit → View Ledger), `ActuarialTranslationEngine.Tests.E2E` leverages **Microsoft Playwright**.
- The `UploadModelTest` class automatically spawns the background API and Web processes via `Process.Start()`.
- It executes a rigorous UI validation loop:
  1. Locates `edu-2012-c13-01.xlsx` and uploads it.
  2. Waits for dynamic Target Sheet evaluation.
  3. Begins the interactive Session Wizard.
  4. Triggers the Cancellation loop explicitly via button clicks to ensure backend tracking halts properly.
  5. Asserts the "Certified: Zero Variance" visual badges accurately render after history lookups.

## 2. Unit Testing and Mock Separation
The `Tests.Unit` project focuses purely on execution logic.
- We utilize `Microsoft.EntityFrameworkCore.InMemory` to rapidly assert against `SqlitePersistenceManager` behaviors without locking I/O channels.
- Mocking the LLM: Tests use `MockDomainInterrogationBridge` implementations that guarantee pre-defined generic JSON payloads. We test *the Engine's ability to compile and orchestrate*, isolated entirely from external latency loops or rate limits.

## 3. Resilience and Failure Testing
Unit tests enforce Polly implementations explicitly:
- We test that `BackgroundTranslationWorker` handles `OperationCanceledException` properly.
- We test that `StartupSweeper` accurately identifies database nodes missing file artifacts and clears them accordingly.
