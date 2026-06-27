# 07 Observability and UI Governance

The platform features a dense, interactive frontend built in Blazor (Server Mode) that ties directly into the execution backends.

## 1. Real-Time Observability via SignalR
A massive, long-running extraction requires live visibility. Instead of a spinning icon, we implemented real-time `IProgress<TranslationProgressEvent>`.
- **The TranslationProgressHub:** An ASP.NET Core SignalR hub that broadcasts live state messages ("Extracting Row 14", "Compiling Roslyn Model", "Variance Failed").
- **Visual Feedback:** The `Home.razor` UI updates an animated progress bar based on `event.PercentComplete`. It simultaneously appends logs to a `<textarea>` live-feed window.

## 2. Session Cancelation Capabilities
Users maintain direct control over heavy compute usage. A dedicated "Cancel Translation" button triggers an endpoint (`DELETE /api/evaluate/{jobId}`) that flips the server-side `CancellationToken`, instantly shutting down the background execution block and recovering the hardware.

## 3. The Audit Ledger and Data Provenance
Translation isn't just about outputting code; it's about governance.
- The **AuditHistory.razor** component acts as the public ledger. It dynamically queries the `SqlitePersistenceManager` for historical transactions.
- **Provenance Badges:** The UI renders specific status indicators ("Certified: Zero Variance" vs "Reconciliation Failed"), logging exact target sheets, origin files, timestamps, and LLM models used.
- **One-Click Distribution:** A JSInterop routine (`BlazorDownloadFile`) allows users to instantly package and download the completed `partition_x_translated.cs` models directly out of the SQLite ledger into their local file system for external inclusion into main-line actuarial repos.
