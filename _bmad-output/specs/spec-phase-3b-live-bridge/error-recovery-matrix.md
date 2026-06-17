# Failure Modes & Error Recovery Matrix

| Failure | Detection | Recovery |
|---------|-----------|----------|
| **Network timeout** | `TaskCanceledException` | Retry up to `MaxRetries` with exponential backoff (`RetryDelayMs * 2^attempt`) |
| **Rate limited (429)** | `HttpStatusCode.TooManyRequests` | Extract `Retry-After` header; wait that duration; retry |
| **LLM returns non-compilable C#** | Roslyn `Emit` fails with diagnostics | Re-prompt the LLM with the compilation errors appended to the user message |
| **LLM omits delimiter** | `IndexOf` returns -1 | Retry once with an explicit reminder appended: `"IMPORTANT: You must include ===CSHARP_MIRROR=== between the two sections."` |
| **LLM hallucinates extra namespaces** | Roslyn compilation succeeds but `GetType("DynamicReconciliationUnit")` returns null | Strip all `namespace` declarations from the C# output and retry compilation |
| **Mathematical variance exceeds threshold** | `ActuarialLogicLeakException` | Log the failure with full context (inputs, expected, actual). Do NOT retry — this indicates a genuine semantic misunderstanding by the LLM that requires human review. |
