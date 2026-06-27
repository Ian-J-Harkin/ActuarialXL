# Actuarial Translation Engine: User & Testing Guide

Welcome to the **Actuarial Translation Engine** project! This guide will walk you through how to use the system as an end-user, how developers can test the pipeline, and how the underlying architecture behaves during end-to-end (E2E) translation.

---

## 1. User Guide

The Actuarial Translation Engine provides two primary interfaces for users to translate legacy Excel actuarial models into modern, auditable C# code: a Command Line Interface (CLI) and a Web UI.

### 1.1 Web UI (Governance Dashboard)
The Web UI is a Blazor application that provides a visual interface for uploading models and viewing real-time translation progress.

**How to Use:**
1. Start the API and Web applications from your terminal:
   - To run the API: `dotnet run --project ActuarialTranslationEngine.API`
   - To run the Web UI: `dotnet run --project ActuarialTranslationEngine.Web`
   - *(Optional Developer Tip)*: If you are making UI/HTML changes, run the Web UI using hot-reload instead: `dotnet watch run --project ActuarialTranslationEngine.Web`. This will automatically apply C# and HTML changes without needing to restart the server.
2. Open your browser and navigate to `http://localhost:5200` (or the configured Web UI port).
3. Click the upload zone and select an `.xlsx` or `.xlsm` file (e.g., `edu-2012-c13-01.xlsx`). *(Note: Uploads are strictly limited to 5MB and must be valid Excel archives).*
4. The system will dynamically inspect the file. Select the **Target Sheet** from the populated dropdown. You *must* select a specific sheet; bulk-processing an entire workbook is restricted to prevent resource exhaustion.
5. Click **Create Translation Session**.
6. The **Interactive Translation Wizard** will appear, listing the extracted partitions for your selected sheet. Click the **Translate** button next to the specific partition you want to process.
7. The system will display a real-time progress bar and a scrolling log window as it processes that partition.
8. Once completed, the button will change to **View Ledger**. Click it to open the Governance Dashboard and view the generated C# runtime code side-by-side with the semantic specification.
   - **Provenance Badge:** Each run displays an Audit Ledger badge indicating the exact Database ID, Timestamp, and LLM Model used for the translation.
   - **Data Provenance:** The generated C# code now clearly identifies the precise mathematical source via a dynamic `Compiled` header (e.g., `Compiled: Worksheet Table 13.4 (Rows 5-20)` or `VBA Module: Module1`). This ensures the compiled block can be traced 1:1 directly back to its originating Excel workbook range or macro.
   - **Export:** You can click the **Download .cs** button on any generated partition. Instead of saving to the server's hard drive, the file is generated directly in the browser and saved to your local machine's `Downloads` folder.
   - **State Recovery:** You can safely refresh the page or close your browser tab during a long-running translation. The UI saves the active Job ID in `localStorage` and will automatically re-connect to the live SignalR feed or fetch the final results when you return.

### 1.2 Audit History
The system securely streams and saves every evaluation block to a local SQLite database (`audit.db`). You can retrieve past translations by clicking **Audit History** in the left navigation menu.
- **Audit Ledger:** Lists all previous translation runs, their timestamps, running status (Completed, Failed, Canceled), and the number of logical partitions generated. The ledger is fully paginated.
- **Artifact Retrieval:** Click "View Artifacts" to load the historical code into the dashboard without re-running the LLM pipeline.

### 1.3 CLI (Command Line Interface)
The CLI is a powerful tool for batch processing, automated testing, and interacting directly with the core engine without the Web UI overhead.

**How to Use:**
Navigate to the root directory and run the following command to execute the translation pipeline for a specific sheet:

```bash
dotnet run --project ActuarialTranslationEngine.CLI -- --file "edu-2012-c13-01.xlsx" --sheet "Solution to Exercise 18.10" --e2e
```

**Key CLI Flags:**
- `--file <path>`: The path to the target Excel workbook.
- `--sheet <name>`: The specific worksheet name to translate.
- `--e2e`: Triggers the live LLM integration, Roslyn compilation, and empirical data reconciliation.
- `--verbose`: Outputs detailed logs, including the raw JSON payloads sent to the LLM.

**Output:**
If the `--e2e` flag is provided, the CLI will output the final translated C# classes directly to the `./output` folder in your current directory.

---

## 2. Testing Guide

The system uses a highly defensive "Triple Lock" testing and reconciliation strategy to guarantee that the LLM-generated code perfectly matches the original Excel mathematics.

### 2.1 E2E Pipeline Verification
When you run the `--e2e` flag via the CLI, the system executes the **Stateful Hybrid Reconciliation** loop.

1. **Extraction & Compression:** The system extracts the Excel logic and compresses it into logical blocks (Partitions).
2. **Translation:** The engine sends one partition at a time to the LLM (via `OpenRouter/Claude`).
3. **Compilation:** The returned C# is compiled in-memory using `Roslyn`.
4. **Row-by-Row Reconciliation:**
   - The engine iterates through the original Excel rows.
   - It executes the compiled C# method using the inputs from the Excel file.
   - It compares the C# output to the original Excel output. If there is a variance > `0.00m`, the test fails.
   - **Ground-Truth Resetting:** After every row, the historical state is reset to the exact Excel values to prevent compounding floating-point drift.

At the end of an E2E run, a final execution summary is printed:
```text
========================================================
FINAL E2E EXECUTION SUMMARY
========================================================
SHEET NAME                     | STATUS     | ERROR
--------------------------------------------------------------------------------
Solution to Exercise 18.10     | SUCCESS    | 
========================================================
```

### 2.2 Running Automated Unit Tests
The project contains a comprehensive suite of Unit and Integration tests.

To run the test suite, use the standard .NET CLI command:
```bash
dotnet test ActuarialTranslationEngine.slnx
```

**Key Test Projects:**
- **`ActuarialTranslationEngine.Tests.Unit`**: Tests core domain logic, Excel parsing (`ActuarialExtractionEngine`), and Vector compression.
- **`ActuarialTranslationEngine.Tests.E2E`**: Dedicated E2E integration tests using Playwright. 

**Running Specific Test Suites:**
To run only the unit tests (very fast, no LLM):
```bash
dotnet test ActuarialTranslationEngine.Tests.Unit
```

To run the full E2E Web UI Playwright tests:
```bash
# Note: Live LLM tests require valid API keys (see section 2.3)
dotnet test ActuarialTranslationEngine.Tests.E2E
```

**Note on Concurrency:** The test suite is fully resilient to parallel multi-threaded execution. E2E Playwright tests dynamically resolve ephemeral ports to prevent HTTP listener collisions (`ERR_CONNECTION_REFUSED`), and MSBuild `ProjectReference` dependency chaining prevents `testhost.exe` from locking DLLs during test bootstrapping.

### 2.3 Required Environment Variables
For E2E LLM testing, you must provide your API keys to the environment before running the CLI or the API.

```bash
# Windows (PowerShell)
$env:ACTUARIAL_LLM_API_KEY="your-openrouter-api-key"
$env:ACTUARIAL_LLM_ENDPOINT="https://openrouter.ai/api/v1/chat/completions"
```

### 2.4 Known Limitations & Troubleshooting
Because the E2E pipeline relies on live network calls to an external LLM (e.g., OpenRouter, OpenAI), you may encounter transient network instability, especially when processing many partitions in sequence.

**Common Error: Translation Timeouts and Long Processing Times**
```text
TaskCanceledException or OperationCanceledException during LLM evaluation.
```
**Why this happens:** A multi-partition spreadsheet (e.g., `edu-2012-c13-01.xlsx` with 6 partitions) requires 6 sequential LLM calls plus 6 Roslyn compilation passes. This can take several minutes.
**How it is handled:** The architecture uses an asynchronous job pattern. When the UI uploads a file, the API instantly returns a `202 Accepted` response with a `JobId`. The translation is processed in a background queue (`BackgroundTranslationWorker`) with `SemaphoreSlim(3)` throttling to prevent LLM rate limiting. Progress is pushed to the UI via a SignalR WebSocket. Furthermore, results are streamed to the SQLite database incrementally per partition to prevent data loss on memory failures. If a specific LLM call times out mid-partition, the `ReconciliationOrchestrator` catches the timeout and uses exponential backoff to retry up to 3 times before failing the job.

**Common Error: Connection Aborted (SocketException 10053)**
```text
System.Net.Http.HttpRequestException: Error while copying content to a stream. 
---> System.IO.IOException: Unable to write data to the transport connection: An established connection was aborted by the software in your host machine.
```
**Why this happens:** This occurs when the external LLM provider drops the connection (due to rate limiting, payload timeouts, or upstream network resets).
**Workaround:** The engine implements automatic exponential backoff retries using `Polly` to gracefully retry network and socket layer exceptions without crashing the background worker.



---

## 3. Running the System

To boot the full system for local testing and Web UI usage, start both the API and Web projects.

**Terminal 1 (Start the API):**
```bash
dotnet run --project ActuarialTranslationEngine.API
```
*Note: Once the API is running, developers can view and interact with the REST endpoints via the Swagger UI at `http://localhost:5242/swagger`.*

**Terminal 2 (Start the Web UI):**
```bash
dotnet run --project ActuarialTranslationEngine.Web
```

The Web UI communicates with the API over HTTP and establishes a SignalR WebSocket connection to stream live `TranslationProgressEvent` ticks directly to your browser!
