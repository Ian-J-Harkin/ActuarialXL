# 05 Roslyn Reconciliation Sandbox

The defining validation boundary of the Actuarial Semantic Translation Engine is the automated Roslyn compilation layer. The Engine never assumes an LLM output is correct; it physically executes it against historical spreadsheets and checks the mathematical reality.

## 1. Abstract Syntax Tree Parsing
When the LLM yields its `===CSHARP_MIRROR===` payload, the `RoslynReconciliationEngine` slices out the raw text and utilizes `CSharpSyntaxTree.ParseText()`. 
- **Strict Dependencies:** The compilation options explicitly link standard CLR libraries and the `IActuarialReconciliationUnit` contract.
- **Emission Targets:** The syntax tree is emitted into an active MemoryStream (`.dll`) and inspected for build diagnostics. If a compilation error occurs, the engine throws an `ActuarialDynamicCompilationException` and re-queues the generation with the failure log.

## 2. The Collectible Sandbox Pattern (Anti-Leak)
Loading dynamic assemblies continuously inside an ongoing application introduces severe `MemoryLeak` risks. Types loaded into the default AppDomain can never be unloaded, eventually crashing the Background Worker.
- **Solution:** We wrap all dynamic executions in a dedicated `AssemblyLoadContext("ActuarialValidationScope", isCollectible: true)`.
- **Garbage Collection Hooks:** When the validation cycle finishes (pass or fail), `isolatedContext.Unload()` is explicitly invoked, freeing the server's RAM and maintaining a flat memory profile across parallel executions.

## 3. Penny-Perfect Variance Verification
Once compiled, the resulting instance is fed 3 deterministic rows from the active partition boundaries (First, Mid, Last).
- It executes `decimal calculated = instance.ExecuteCalculationRow(inputs)`.
- The engine calculates `Math.Abs(calculated - excelGroundTruth)`.
- **The Actuarial Mandate:** If variance exceeds $\le 0.00001m$, the system flags a severe `ActuarialLogicLeakException` and rejects the LLM translation.
