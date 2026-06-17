# Enterprise Evolutionary Lifecycle Specification: Actuarial Semantic Translation Engine

---

## Phase I: The Throwaway Feasibility Spike (Dual-Hypothesis Testing)

### 1. Architectural Intent

To validate the two core technical prerequisites of the entire platform simultaneously under a zero-abstraction environment:

* **Hypothesis A (.NET Extraction Fidelity):** Can `ClosedXML` natively parse the target legacy Excel file format, locate cross-tab ranges, and extract pristine formula strings and evaluated values without throwing exceptions?
* **Hypothesis B (LLM Semantic Accuracy):** Can a modern LLM reliably translate an abstract mathematical expression string into a conceptually correct actuarial description without hallucinating financial intent?

> [!CAUTION]
> **Architectural Mandate 1: Ground Truth Verification**
> Under no circumstances may an execution agent proceed with architectural assumptions, coordinate addresses, or formula strings derived solely from documentation without first empirically interrogating the source `.xlsx` datasets. All technical specs are hypotheses until proven against the structural reality of the raw file.

> [!IMPORTANT]
> **Architectural Mandate 2: Failure Recovery & Reporting Milestones**
> Each time execution hits a snag or unexpected failure in future phases, the execution agent must systematically:
> 1. **Re-ground in Structure:** Immediately re-verify the raw `.xlsx` spreadsheet data to ensure the code's structural assumptions match reality.
> 2. **Sanity Check the LLM:** Isolate the prompt causing the issue and observe the raw JSON/text output to ensure the LLM hasn't drifted or hallucinated.
> 3. **Document the Rationale:** Generate a persistent `docs/PhaseX_Testing_and_Rationale.md` (where X is the current phase) to permanently log the failure, the investigation, and the architectural resolution, modeled identically after the Phase 1 testing document.

This project is structurally isolated and built to be deleted upon exit criteria fulfillment. No production scaffolding is permitted here.

### 2. Solution Topology & Namespacing

* **Project Name:** `ActuarialTranslationEngine.PoC`
* **Project Type:** .NET 8/9 **xUnit Test Project** (Single project, headless).
* **Dependencies:** `ClosedXML` (v0.102.x or higher), `System.Net.Http.Json`.

### 3. Target Dataset Boundary

* **Source File:** `edu-2012-c13-01.xlsx` (Target Sheet: `Example 13.2 - Table 13.4`).
* **Isolated Testing Target:** Row 6, Column N (`Fund Value End of Month`).
* **Expected Raw Formula (ClosedXML Format):** `(N5+D6-K6)*(1+Notes!B3)` (Note: Excel formula syntax is preserved without the leading `=` prefix per ClosedXML native serialization patterns).

### 4. Code & Prompt Specification

The spike must explicitly execute and assert on **Hypothesis A** using a numeric range tolerance and key token matching before invoking **Hypothesis B**:

```csharp
namespace ActuarialTranslationEngine.PoC
{
    using Xunit;
    using ClosedXML.Excel;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;

    public class DualHypothesisSpike
    {
        [Fact]
        public async Task Phase1_Must_Validate_Extraction_Fidelity_And_Semantic_Accuracy()
        {
            // --- HYPOTHESIS A: .NET EXTRACTION FIDELITY ---
            const string filePath = "edu-2012-c13-01.xlsx";
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheet("Example 13.2 - Table 13.4");

            // Extract Target Cell (Row 6, Column N)
            var targetCell = sheet.Cell(6, 14);
            string extractedFormula = targetCell.FormulaA1; // ClosedXML excludes the leading '=' character
            string evaluatedValue = targetCell.Value.ToString();

            // Strict Assertion: Ensure the formula token is parsed and contains structural components
            Assert.True(targetCell.HasFormula, "ClosedXML failed to recognize the formula token context.");
            Assert.False(string.IsNullOrWhiteSpace(extractedFormula), "Formula extraction returned empty string.");
            Assert.Contains("N5", extractedFormula); // Verifies sequential prior-row lookback token exists

            // Numeric tolerance guard: Avoid runtime string variations across OS/precision layers
            double numericValue = double.Parse(evaluatedValue);
            Assert.InRange(numericValue, 953.6558, 953.6560);

            // --- HYPOTHESIS B: LLM SEMANTIC ACCURACY ---
            var rawPayload = new
            {
                TargetCell = "Table_13.4!N6",
                Formula = extractedFormula,
                EvaluatedValue = evaluatedValue,
                SurroundingContext = new[] {
                    new { Cell = "N5", Definition = "Fund Value Prior Month", Value = sheet.Cell(5, 14).Value.ToString() },
                    new { Cell = "D6", Definition = "Net Premium Current Month", Value = sheet.Cell(6, 4).Value.ToString() },
                    new { Cell = "K6", Definition = "Monthly Deduction Current Month", Value = sheet.Cell(6, 11).Value.ToString() }
                }
            };

            string systemPrompt = "You are a Senior Actuary. Translate the provided spreadsheet formula string into its core financial rule. Identify the product framework and map cell coordinates to standard actuarial terms. Output text only.";

            // Endpoint URL and API key are resolved via environment variables or a local secrets configuration.
            string endpointUrl = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_ENDPOINT")
                ?? "https://api.openai.com/v1/chat/completions";
            string apiKey = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_API_KEY")
                ?? throw new InvalidOperationException("ACTUARIAL_LLM_API_KEY environment variable is not set. Cannot execute Hypothesis B.");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.PostAsJsonAsync(endpointUrl, new {
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = JsonSerializer.Serialize(rawPayload) }
                }
            });

            string responseContent = await response.Content.ReadAsStringAsync();

            // Assert: Confirm semantic recognition boundaries
            Assert.Contains("Recursive", responseContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Universal Life", responseContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Fund Value", responseContent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

### 5. Phase I Exit & Knowledge Capture Criteria

* **Execution Pass:** Test passes successfully with zero manual source file mutations.
* **Persistent Artifact Capture:** The validated prompt syntax, structural configuration formats, and LLM reasoning quirks must be committed to the root repository as a persistent markdown file named `/docs/governance/master-prompt-engineering-log.md` before archiving or removing the scratchpad project.

---

## Phase II: The Small Working Pipeline (The Vertical Slice)

### 1. Architectural Intent

Establish formal, production-grade decoupled layers. Implement automated structural serialization and vertical column-collapsing logic across single-period recursion chains ($t-1$).

* **LLM Boundary Rule:** **Strictly deterministic and cost-free.** Live network API calls to the LLM are entirely out of scope. The orchestration tier must exclusively utilize a deterministic `MockDomainInterrogationBridge` to return frozen specification models, allowing the unit test suite to remain fast and isolated.

### 2. Solution Topology & Namespacing

Initialize the core multi-project solution structure:

* `ActuarialTranslationEngine.Core` (**Class Library**): Models, schemas, DTOs, interfaces.
* `ActuarialTranslationEngine.Engine` (**Class Library**): Concrete implementations for parsing and compression.
* `ActuarialTranslationEngine.Tests.Unit` (**xUnit Test Project**): Targeted engine validation.
* *Testing Trait Directive:* Pipeline and file-reading integration-style tests live temporarily inside this project but must be isolated via the `[Trait("Category", "Integration")]` attribute marker.

### 3. Target Dataset Boundary

* **Source Asset:** `edu-2012-c13-01.xlsx` (Target Sheet: `Example 13.2 - Table 13.4`).
* **Archetype Scope:** Strictly **Archetype A** (Time-Series Roll-Forward Loops). Max vertical layout processing depth: 100 continuous rows.

### 4. Code & Contract Specification

#### Core Models & DTO Stubs (`ActuarialTranslationEngine.Core/Models`)

```csharp
namespace ActuarialTranslationEngine.Core.Models
{
    using System.Collections.Generic;

    public class ColumnDefinition
    {
        public required string ColumnLetter { get; init; }
        public required string ExtractedHeaderName { get; init; }
        public string TokenizedFormulaTemplate { get; set; } = string.Empty;
        public List<int> ChronologicalLookbacks { get; set; } = new();
    }

    public class RawWorkbookMap
    {
        public string SheetName { get; set; } = string.Empty;
        public List<RawRowMetadata> DataRows { get; init; } = new();
    }

    public class RawRowMetadata
    {
        public int RowIndex { get; set; }
        public Dictionary<string, string> CellFormulas { get; set; } = new(); // Key: ColumnLetter
        public Dictionary<string, string> CellValues { get; set; } = new();   // Key: ColumnLetter
    }

    public class CompressedVectorBlock
    {
        public string TargetWorksheet { get; set; } = string.Empty;
        public string ProcessingArchetype { get; set; } = string.Empty;
        public List<VectorRangePartition> Partitions { get; set; } = new();
    }

    public class VectorRangePartition
    {
        public int StartRow { get; set; }
        public int EndRow { get; set; }
        public string RangeId { get; set; } = string.Empty;
        public string FormulaSignature { get; set; } = string.Empty; // Used by Phase III-A change-point detection
        public List<ColumnDefinition> StructuralColumns { get; set; } = new();
    }

    public class TranslationOutput
    {
        public string FinalAuditableMarkdown { get; set; } = string.Empty;
        public string GeneratedCSharpMirrorCode { get; set; } = string.Empty;
    }
}
```

#### Core Interfaces (`ActuarialTranslationEngine.Core/Interfaces`)

```csharp
namespace ActuarialTranslationEngine.Core.Interfaces
{
    using System.IO;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using ActuarialTranslationEngine.Core.Models;

    public interface IActuarialExtractionEngine
    {
        RawWorkbookMap ExtractSheetData(Stream fileStream, string sheetName);
    }

    public interface IVectorCompressionEngine
    {
        CompressedVectorBlock CompressTopology(RawWorkbookMap rawMap);
    }

    using System.Threading;

    public interface IDomainInterrogationBridge
    {
        Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string? previousCompilerError = null, CancellationToken cancellationToken = default);
    }

    public interface IActuarialReconciliationUnit
    {
        decimal ExecuteCalculationRow(Dictionary<string, decimal> inputs);
    }

    // NOTE: Evolved from synchronous to asynchronous (Task-based) in Phase III-B 
    // to properly support non-blocking syntax tree parsing, compilation steps, 
    // and cancellation token support during execution timeouts.
    public interface IRoslynReconciliationEngine
    {
        Task CompileAndVerifyAsync(string csharpCode, Dictionary<string, decimal> rowInputs, decimal expectedSpreadsheetResult, CancellationToken cancellationToken = default);
    }
}
```

#### Core Exceptions (`ActuarialTranslationEngine.Core/Exceptions`)

```csharp
namespace ActuarialTranslationEngine.Core.Exceptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.CodeAnalysis;

    public class ActuarialLogicLeakException : Exception
    {
        public ActuarialLogicLeakException(string message) : base(message) { }
    }

    public class ActuarialDynamicCompilationException : Exception
    {
        public IEnumerable<Diagnostic> Diagnostics { get; }

        public ActuarialDynamicCompilationException(IEnumerable<Diagnostic> diagnostics)
            : base($"Diagnostic errors encountered during runtime compilation: {string.Join("; ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}")
        {
            Diagnostics = diagnostics;
        }
    }
}
```

#### Deterministic Mock Integration Boundary (`ActuarialTranslationEngine.Engine/Bridges`)

```csharp
namespace ActuarialTranslationEngine.Engine.Bridges
{
    using System.Threading.Tasks;
    using ActuarialTranslationEngine.Core.Interfaces;
    using ActuarialTranslationEngine.Core.Models;

    public class MockDomainInterrogationBridge : IDomainInterrogationBridge
    {
        public Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload)
        {
            return Task.FromResult(new TranslationOutput
            {
                FinalAuditableMarkdown = "# Mock Actuarial Function Specification\nVerified Baseline Row Output.",
                GeneratedCSharpMirrorCode = "using System; using System.Collections.Generic; using ActuarialTranslationEngine.Core.Interfaces; public class DynamicReconciliationUnit : IActuarialReconciliationUnit { public decimal ExecuteCalculationRow(Dictionary<string, decimal> inputs) => 953.6558m; }"
            });
        }
    }
}
```

### 5. Phase II Exit Criteria

* The structural `VectorCompressionEngine` successfully ingests a multi-row map from the Excel file and outputs a verified `CompressedVectorBlock` schema payload containing **no more than 2 `VectorRangePartition` blocks**: 1 initialization seed row (hardcoded values or structurally unique first-row formula) and 1 continuous recursive body. If the extraction boundary excludes initialization rows, precisely 1 partition is expected.
* **Validation Target:** Mock dependencies and unit test the compression logic on all four Phase I archetypes.
* **Exit Criteria:** The `.NET` test suite correctly collapses the `Table 13.4` mock into a JSON structure featuring 14 distinct logical columns, with no more than 3 distinct mathematical partitions.

### Phase II-B: VBA Code-to-Code Ingestion Pipeline (Addendum)
Because legacy actuarial workbooks often contain embedded VBA macros that dictate non-linear procedural logic, standard cell-grid extraction is insufficient. VBA logic cannot be parsed using simple cell-dependency graphs because it introduces procedural loops, side effects, and state mutations that don't exist in a flat Excel grid.

This phase will execute the **Code-to-Code Translation Pattern**:
1. **Extraction:** A lower-level ingestion layer (utilizing `DocumentFormat.OpenXml` or `OpenMcdf`) will access the `XLWorkbook.MacroWorkbook` stream to read the raw text code of the underlying `.vbaProject.bin` modules as plain strings, bypassing `ClosedXML`'s limitations.
2. **Contextual Payload:** The raw VBA text strings, along with their surrounding cell grid variable maps, are injected into the JSON payload.
3. **LLM Translation:** The LLM acts as a code-compiler, stripping legacy VB6 keywords, global state dependencies, and visual sheet-selection side effects (e.g., `.Select`). It flattens procedural VBA loops into stateless, pure functions.
4. **Dynamic C# Target:** The LLM outputs clean, object-oriented C# methods wrapped in the standardized `IActuarialReconciliationUnit` interface, ready to be ingested by Phase III-B.

### Phase III-A: The CLI Orchestrator (Multi-Archetype Expansion)

### 1. Architectural Intent

To eliminate parsing errors and expand extraction/compression mechanics to support the entire spectrum of actuarial design patterns found within the textbook dataset.

* **LLM Boundary Rule:** Continues using the **Mock Bridge** to guarantee that any failure in this phase is strictly isolated to ingestion/parsing logic rather than prompt or generation variance.
* **Domain Model Note:** This phase consumes the extended `VectorRangePartition.FormulaSignature` string property to execute change-point detection validations.

### 2. Solution Topology & Namespacing

* Append `ActuarialTranslationEngine.CLI` (**Console Application**): Provides a local desktop command-line harness for testing extraction passes directly on raw files.

### 3. Target Dataset Boundary

* **Source Sheets:** `Example 18.4 - Table 18.8` (Dense, multi-ledger balancing columns tracking benefit, expense, and DAC reserves) and `Example 16.3 - Part 1` (Stochastic distribution arrays featuring volatile functions and native error anomalies).

### 4. Code & Contract Specification

The compression pass must implement continuous change-point algorithms using an explicit column key-sorted array join to ensure dictionary ordering variations do not generate phantom change-points:

```csharp
namespace ActuarialTranslationEngine.Engine.Compression
{
    using System.Linq;
    using System.Collections.Generic;
    using ActuarialTranslationEngine.Core.Models;
    using ActuarialTranslationEngine.Core.Interfaces;

    public class VectorCompressionEngine : IVectorCompressionEngine
    {
        public CompressedVectorBlock CompressTopology(RawWorkbookMap rawMap)
        {
            var compressedBlock = new CompressedVectorBlock { TargetWorksheet = rawMap.SheetName };
            VectorRangePartition activePartition = null;

            foreach (var row in rawMap.Rows.OrderBy(r => r.RowNumber))
            {
                // Key-sorted ordering guarantees deterministic evaluations regardless of insertion sequence
                string formulaSignature = GenerateAbstractFormulaSignature(row);

                if (activePartition == null || activePartition.FormulaSignature != formulaSignature)
                {
                    if (activePartition != null) activePartition.EndRow = row.RowNumber - 1;

                    activePartition = new VectorRangePartition {
                        StartRow = row.RowNumber,
                        FormulaSignature = formulaSignature,
                        RangeId = $"Partition_Pivot_At_Row_{row.RowNumber}"
                    };
                    compressedBlock.Partitions.Add(activePartition);
                }
            }
            if (activePartition != null) activePartition.EndRow = rawMap.Rows.Max(r => r.RowNumber);
            return compressedBlock;
        }

        private string GenerateAbstractFormulaSignature(RawRowMetadata row)
        {
            // Deterministic key sorting prevents non-deterministic dictionary ordering from breaking change-points
            return string.Join("|", row.CellFormulas.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));
        }
    }
}
```

### 5. Phase III-A Exit Criteria

* The CLI cleanly processes all four structural archetypes (Time-series, stochastic, multi-ledger, variable payout) out of `edu-2012-c13-01.xlsx` and generates structurally pristine JSON documents featuring zero unmapped cell addresses or unhandled exceptions.

---

## Phase III-B: Dynamic Roslyn Compilation & Reconciliation Core

### 1. Architectural Intent

To light up the automated mathematical validation layer. This phase introduces live generative capabilities and verifies that the system can compile code strings dynamically in memory and execute them against spreadsheet baselines down to a fraction of a penny.

* **LLM Boundary Rule:** **Live Bridge Active.** Swap the mock integration layer for the live endpoint. The live bridge reads and compiles the markdown-based prompt template from the path `/docs/governance/master-prompt-engineering-log.md` into memory during service initialization.
* **Runtime Sandbox Rule:** Initialize the isolated `AssemblyLoadContext` pattern immediately from the start of this phase. The transition to Phase IV will merely append the `isCollectible: true` constructor flag and wrap the context lifecycle inside a standard `using` block, completely avoiding downstream structural code rewrites.

### 2. Solution Topology & Dependencies

* Add a project reference to `Microsoft.CodeAnalysis.CSharp` (Roslyn) within the `ActuarialTranslationEngine.Engine` project library boundary.

### 3. Target Dataset Boundary

* Known-good JSON payloads generated from the certified structural outputs of Phase III-A.

### 4. Code & Contract Specification

The reconciliation loop dynamically loads compiled byte arrays into runtime assemblies using a mandatory explicit reference tree to prevent silent compilation failures:

```csharp
namespace ActuarialTranslationEngine.Engine.Roslyn
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.Loader;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using ActuarialTranslationEngine.Core.Interfaces;
    using ActuarialTranslationEngine.Core.Exceptions;

    public class RoslynReconciliationEngine : IRoslynReconciliationEngine
    {
        public void CompileAndVerify(string cSharpSourceCode, decimal expectedSpreadsheetResult, Dictionary<string, decimal> rowInputs)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(cSharpSourceCode);

            // Explicitly declare the complete metadata reference tree to prevent silent compilation errors
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),                           // Core CLR types
                MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),                    // Generic collections
                MetadataReference.CreateFromFile(typeof(IActuarialReconciliationUnit).Assembly.Location),     // Platform core contracts
                MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location)  // Primitive type mappings
            };

            var compilation = CSharpCompilation.Create("DynamicActuarialUnit")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(syntaxTree);

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                throw new ActuarialDynamicCompilationException(emitResult.Diagnostics);
            }

            // ARCHITECT OVERRIDE (Phase III-B Risk Analysis): isCollectible is true from Phase III-B onwards
            // See architectural-blueprint.md §9.4 for rationale
            using var isolatedContext = new AssemblyLoadContext("ActuarialValidationScope", isCollectible: true);

            ms.Position = 0;
            Assembly assembly = isolatedContext.LoadFromStream(ms);

            var type = assembly.GetType("DynamicReconciliationUnit");
            var instance = (IActuarialReconciliationUnit)Activator.CreateInstance(type)!;

            decimal ruleCalculatedResult = instance.ExecuteCalculationRow(rowInputs);

            // Enforce penny-perfect variance evaluation criteria
            decimal varianceDelta = Math.Abs(ruleCalculatedResult - expectedSpreadsheetResult);
            if (varianceDelta > 0.00001m)
            {
                throw new ActuarialLogicLeakException($"Mathematical regression failure. Variance delta: {varianceDelta}");
            }
        }
    }
}
```

### 5. Phase III-B Exit Criteria

* The pipeline dynamically compiles live LLM output strings, executes back-testing calculations, and passes regression audits against the entire test ledger suite within the specified $\le 0.00001$ variance ceiling.

---

## Phase III-C: Imperative VBA Logic Extraction & Translation (The Dynamo Addendum)

### 1. Architectural Intent
To expand the translation engine beyond declarative cell formula networks (Archetypes A-D) into imperative, state-mutating legacy code. This phase proves the platform can extract raw VBA binary streams, parse imperative loops (e.g., Monte Carlo simulations), and translate them into stateless, deterministic C# implementations that fit into our `IActuarialReconciliationUnit` sandbox.

### 2. Solution Topology & Dependencies
**Dependency Addition:** Append `DocumentFormat.OpenXml` to the `ActuarialTranslationEngine.Engine` project. ClosedXML cannot read the `vbaProject.bin` binary stream; OpenXml is required to natively extract the macro source text from `.xlsm` and `.xlsb` files without requiring an Excel interop process.

### 3. Target Dataset Boundary
* **Source Asset:** CAS Public Access DFA Model (Dynamo v4.1) (`.xlsb` or `.xlsm` format).
* **Archetype Scope:** **Archetype E (Imperative State Mutation)**. Workbooks where the core actuarial logic does not live in cell formulas, but rather in VBA modules that read inputs, compute in memory, and overwrite dashboard cells.

### 4. Code & Contract Specification

#### The VBA Extraction Contract (`ActuarialTranslationEngine.Core/Interfaces`)
The pipeline must route macro-enabled files through an additional binary extraction phase.

```csharp
namespace ActuarialTranslationEngine.Core.Interfaces
{
    using System.IO;
    using System.Collections.Generic;

    public interface IVbaExtractionEngine
    {
        // Returns a dictionary where Key = Module Name, Value = Raw VBA Source Text
        Dictionary<string, string> ExtractMacroModules(Stream fileStream);
    }
}
```

#### The OpenXml Extraction Implementation (`ActuarialTranslationEngine.Engine/Parsers`)

```csharp
namespace ActuarialTranslationEngine.Engine.Parsers
{
    using System.IO;
    using System.Collections.Generic;
    using DocumentFormat.OpenXml.Packaging;
    using ActuarialTranslationEngine.Core.Interfaces;

    public class VbaExtractionEngine : IVbaExtractionEngine
    {
        public Dictionary<string, string> ExtractMacroModules(Stream fileStream)
        {
            var modules = new Dictionary<string, string>();
            
            // OpenXml targets the vbaProject.bin part natively
            using var spreadsheetDocument = SpreadsheetDocument.Open(fileStream, isEditable: false);
            var vbaProjectPart = spreadsheetDocument.WorkbookPart?.VbaProjectPart;

            if (vbaProjectPart == null) return modules; // No macros present

            using var vbaStream = vbaProjectPart.GetStream();
            using var reader = new StreamReader(vbaStream);
            string rawVbaBinaryText = reader.ReadToEnd();

            // NOTE: Agent must implement standard binary regex/parsing here 
            // to split rawVbaBinaryText into distinct Module blocks (e.g., "Attribute VB_Name = ...")
            modules.Add("Extracted_VBA_Payload", rawVbaBinaryText); 
            
            return modules;
        }
    }
}
```

#### LLM Prompt Modification (Imperative Translation)
When `VBAMacroDependency` is flagged, the LLM prompt must shift. It can no longer assume a single cell output.

```text
SYSTEM INSTRUCTIONS FOR VBA TRANSLATION:
You are analyzing an extracted legacy VBA module from an actuarial spreadsheet.
Unlike cell formulas, this code represents imperative state mutation.

1. Trace the Monte Carlo or Simulation Loops.
2. Identify the Input Range addresses and Output Range addresses.
3. Translate the VBA logic into the standard `IActuarialReconciliationUnit` C# class. 
   - You MUST replace VBA multidimensional arrays and Collections with native C# generic arrays or Lists.
   - Output the C# code under the strict ===CSHARP_MIRROR=== delimiter.
```

### 5. Phase III-C Exit Criteria
* **Extraction Pass:** The CLI successfully targets the CAS Dynamo `.xlsm`/`.xlsb` file and extracts the macro binary stream into readable string variables without throwing an `OpenXmlPackageException`.
* **Compilation Pass:** The LLM Interrogation Core translates a target VBA subroutine (e.g., a claims reserving loop) into C#, and the Roslyn engine successfully compiles the resulting code into memory without syntax errors.

---

## Phase IV: Full Enterprise-Grade Architecture (Production API)

### 1. Architectural Intent

Expose the certified parsing, compression, and reconciliation core as a secure, high-throughput, multi-tenant enterprise microservice framework.

### 2. Solution Topology & Namespacing

* Append `ActuarialTranslationEngine.API` (**ASP.NET Core WebAPI Project** utilizing Minimal APIs).
* Pull the integration traits out of the primary test assembly and scaffold `tests/ActuarialTranslationEngine.Tests.Integration` as an independent, standalone execution test project.

### 3. Target Dataset Boundary

* Simultaneous, multi-user production pipelines processing real-world, concurrent streams of massive proprietary life/pension spreadsheets (up to 50MB+ per file) uploaded asynchronously over secure cloud channels.

### 4. Architectural Controls

* **Collectible Memory Sandboxing:** Modify the Phase III-B compilation host block by enabling `isCollectible: true` inside the `AssemblyLoadContext` constructor and wrapping its runtime lifecycle inside an explicit execution boundary block. Call `isolatedContext.Unload()` on validation loop completion to immediately reclaim server memory allocations.
* **Concurrency Gates:** Implement asynchronous task offloading via channels or thread-throttled queues inside the ingestion controller to protect memory pools from exhaustion under simultaneous large file uploads.

### 5. Phase IV Exit Criteria

* The WebAPI successfully processes concurrent multi-tenant validation requests under intensive load testing conditions, while performance telemetry profiles confirm a completely flat system memory and garbage collection footprint over time.

---

## Strategic Summary of Technical Evolution

| Dimension | Phase II: Small Pipeline | Phase III-A: Scope | Phase III-B: Recon | Phase III-C: VBA Logic | Phase IV: Enterprise API | Phase V: Governance UI |
| --- | --- | --- | --- | --- | --- | --- |
| **Primary Project** | Multi-Project Solution | Developer CLI Tool | Roslyn Core Reference | OpenXml Implementation | ASP.NET Core WebAPI | Blazor WASM / Server |
| **Algorithmic Focus** | $t-1$ Vector Compression | Change-Point Ranges | Dynamic Sandbox Comp | Imperative Translation | Collectible Sandboxing | DOM Rendering & State |
| **Data Scope Source** | 1 Sheet (Table 13.4) | All edu-2012 Archetypes | Confirmed Payload Sets | CAS Dynamo Model | Enterprise Workbooks | Novel Actuarial Models |
| **Validation Metric** | JSON Schema Assertion | Data Graph Validation | Variance $\le 0.00001$ | VBA Parsing & Comp | Concurrency Profile | Formal UAT Sign-off |
