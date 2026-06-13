# Enterprise Technical Specification: Actuarial Semantic Translation Engine (.NET Blueprint)

**System Target:** Actuarial Model Reverse-Engineering and Model Risk Governance

**Ecosystem Baseline:** .NET 8 / .NET 9 C# Core Runtime

**Primary Input Corpus:** `edu-2012-c13-01.xlsx` Structural Matrix Sheets

---

## 1. System Architecture Pipeline

The system is designed as a decoupled, deterministic-to-semantic pipeline. It extracts the structural formulas and evaluated results out of Excel natively in .NET, processes the mathematical topology into compressed logical chunks, interprets the rules via an advanced Domain LLM Agent, and reconciles the resulting rules mathematically.

```
+------------------------+      +--------------------------+      +--------------------------+
|  .NET Ingestion Layer  | ---> |  Vector Compression Engine| ---> |   LLM Interrogation Core  |
| (ClosedXML / XLParser) |      | (Time-Series Collapse)   |      |  (Semantic Translation)  |
+------------------------+      +--------------------------+      +--------------------------+
                                                                                |
                                                                                v
                               +--------------------------+      +--------------------------+
                               | Model Governance Output  | <--- |   Reconciliation Loop    |
                               |  (Auditable Markdown)    |      |  (Penny-Perfect Balance) |
                               +--------------------------+      +--------------------------+
                                            |
                                            v
                               +--------------------------+
                               | Actuarial Rules Database |
                               | (SQLite / EF Core JSONB) |
                               +--------------------------+
```

### Phase 1: Structural Extraction

* **Component:** `ActuarialExtractionEngine` (.NET C# class library).
* **Dependencies:** `ClosedXML` (high-performance open-source XML parser) and `XLParser` (for breaking Excel formulas down into Abstract Syntax Trees).
* **Operation:** Scans sheets without Excel execution overhead. It maps the spatial metadata (row headers, column headers, cell coordinate networks) alongside the dual data points of every cell: the raw formula string (`.FormulaA1`) and the last-calculated scalar value (`.Value`).

### Phase 2: Vector Compression (Structural Chunking)

* **Component:** `VectorCompressionEngine`.
* **Operation:** Identifies parallel longitudinal arrays (e.g., repeating month-by-month or year-by-year calculations) and compresses them into a single token-efficient structural block template, eliminating thousands of rows of redundancy.

### Phase 3: Semantic Interrogation

* **Component:** `DomainInterrogationBridge`.
* **Operation:** Orchestrates structured system prompts to pass the token-optimized lineage network to an execution-grade LLM agent, forcing semantic translation instead of compilation.

### Phase 4: Automated Reconciliation Loop

* **Component:** `ReconciliationEngine`.
* **Operation:** Generates an algorithmic mirror of the extracted logic, back-tests it against the original evaluated data points, and outputs a strict variance delta log.

---

## 2. Archetype-Specific Parsing Matrix

The .NET parsing layer must classify incoming worksheets dynamically by running a shape analysis on column definitions and cell relationships. Based on the provided test dataset (`edu-2012-c13-01.xlsx`), the engine must enforce four explicit structural mapping models:

### Archetype A: Time-Series Roll-Forward Loops

* **Target Matches:** `Table 13.4` (Universal Life Fund Value), `Table 13.5` (Variable Universal Life), `Solution to Exercise 13.1`.
* **Core Logic Pattern:** $FV_{End\_of\_Month\_t} = (FV_{End\_of\_Month\_t-1} + Premium_t - Deduction_t) \times (1 + i_t)$
* **Parsing Target:** Isolate row-to-row recursive cells. Trace the column dependencies (`Premium Expense Charge` $\rightarrow$ `Net Premium` $\rightarrow$ `Monthly Deduction` $\rightarrow$ `Interest On Fund Value`).
* **Constraint:** Extract and pass the explicit initialization state (Row 1 / Month 1 variables) and the general recurrence iteration rule state (Row $n$ relative to Row $n-1$).

### Archetype B: Stochastic Scenario ledgers

* **Target Matches:** `Example 16.3 - Part 1` & `Part 2` (Stochastic Generation of Year 10 Fund Value).
* **Core Logic Pattern:** Random variable mapping across non-linear lookup tables (`Look Up Value` array mapping to discrete `Return` scales based on probabilistic distribution intervals).
* **Parsing Target:** Extract the static reference boundaries of the lookup table (`Probability`, `Look Up Value`, `Return`) and trace how the dynamic seed (`Random Number` column) dictates row values.

### Archetype C: Multi-Component Balancing Ledgers

* **Target Matches:** `Example 18.4` (GAAP Reserves for a Whole Life Policy), `Solution to Exercise 18.5`.
* **Core Logic Pattern:** Multi-ledger variance equations. Parallel calculation of independent actuarial streams within the same horizontal timeline row.
* **Parsing Target:** Map distinct structural bounding zones across the row:
* *Columns R-U:* Decrement ledger (`In Force BOY`, `Deaths`, `Lapses`, `In Force EOY`).
* *Columns Y-AA:* Cash Flow claims vectors (`Death Benefits`, `Surrender Benefits`).
* *Columns AD-AE:* Valuation Net Benefit Premium & Benefit Reserves ($V^B$).
* *Columns AH-AK:* Deferrable Acquisition Cost (DAC) amortizations ($V^{DAC}$).
* *Columns AN-AR:* Expense Reserves accumulation ($V^{EXP}$).



### Archetype D: Variable Payout Adjusters

* **Target Matches:** `Example 13.12` and `Solution to Exercise 13.10` (Variable Payout Annuities).
* **Core Logic Pattern:** Dynamic asset depletion tracking against an Assumed Investment Rate (AIR) threshold.
* **Parsing Target:** Anchor cell parsing. Isolate the target cells for `AIR` (e.g., cell value `0.03`) and trace its exponent scaling pattern down the columns `(1+AIR)^-t` as it influences the `Payment Per Annuitant` vector.

---

## 3. Vector Compression Specification

To prevent context window saturation and model hallucinations, the `VectorCompressionEngine` must reduce repeating actuarial sequences down to a single structural representation.

### Algorithmic Logic for Repeating Row Collapsing (Continuous Change-Point Detection)

1. Initialize a scanning window across the rows of an isolated worksheet block.
2. For each row, abstract the formula string by converting absolute coordinates to relative column offset variables (e.g., change `=C5*(1+D5)` and `=C6*(1+D6)` to local tokens `Col[C]*(1+Col[D])`).
3. **Continuous Change-Point Detection Pass:** The engine must map every single row in the column matrix to an abstract token string. It cannot truncate scanning after 3 rows. Instead, it must group consecutive matching rows into partitioned ranges (e.g., Range_1: Rows 6-15, Range_2: Rows 16-25), passing the unique formula template block for each distinct range.
4. Track the vertical recursive links and store their lookback depth (e.g., $t-1$, $t-12$) in the `ChronologicalLookbacks` array to handle multi-period dependencies.
5. Cease raw cell serialization across that block range. Output a **CompressedVectorBlock** with partitioned ranges to pass to the LLM.

```csharp
namespace ActuarialTranslationEngine.Core.Models
{
    public class CompressedVectorBlock
    {
        public string TargetWorksheet { get; set; } = "Example 18.4";
        public string ProcessingArchetype { get; set; } = "Multi_Component_Balancing_Ledger";
        
        // Tracks the precise segmented ranges where the formula remains deterministic
        public List<VectorRangePartition> Partitions { get; set; } = new();
    }

    public class VectorRangePartition
    {
        public int StartRow { get; set; }
        public int EndRow { get; set; }
        public string RangeId { get; set; } // e.g., "Duration_1_Initialization", "Duration_2_to_10_SelectPeriod"
        public List<ColumnDefinition> StructuralColumns { get; set; } = new();
    }

    public class ColumnDefinition
    {
        public string ColumnLetter { get; set; }
        public string CleanHeaderName { get; set; }
        public string TokenizedFormulaTemplate { get; set; } // Expanded abstract AST expression
        public List<int> ChronologicalLookbacks { get; set; } = new(); // Tracks lookback depths (e.g., 1 for t-1, 12 for t-12)

        public ColumnDefinition(string col, string name, string template, List<int> lookbacks)
        {
            ColumnLetter = col;
            CleanHeaderName = name;
            TokenizedFormulaTemplate = template;
            ChronologicalLookbacks = lookbacks ?? new List<int>();
        }
    }
}
```

---

## 4. Extraction Data Schema (Parser-to-LLM Contract)

The .NET parser must output a strict JSON payload validating against the following structural schema definition before handing context off to the LLM Interrogation Core.

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ActuarialContextPayload",
  "type": "object",
  "properties": {
    "Metadata": {
      "type": "object",
      "properties": {
        "SourceWorkbook": { "type": "string" },
        "TargetSheet": { "type": "string" },
        "ClassificationArchetype": { "type": "string" }
      },
      "required": ["SourceWorkbook", "TargetSheet", "ClassificationArchetype"]
    },
    "GlobalAnchors": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "Coordinate": { "type": "string" },
          "NamedIdentifier": { "type": "string" },
          "Value": { "type": "string" }
        }
      }
    },
    "CompressedVectors": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "RangeAddress": { "type": "string" },
          "VectorLogic": { "type": "string" },
          "SampleEvaluatedRow": {
            "type": "object",
            "additionalProperties": { "type": "string" }
          }
        }
      }
    },
    "DisruptiveNodes": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "Coordinate": { "type": "string" },
          "RawFormula": { "type": "string" },
          "EvaluatedValue": { "type": "string" },
          "ExceptionFlag": { "type": "string" }
        }
      }
    }
  },
  "required": ["Metadata", "GlobalAnchors", "CompressedVectors", "DisruptiveNodes"]
}

```

---

## 5. Domain LLM Agent Interrogation Prompt

This is the exact, finalized operational prompt block to be loaded into the high-power LLM interrogation core agent execution environment.

```text
SYSTEM INSTRUCTIONS: MASTER ACTUARIAL SPECIFICATION TRANSLATOR
ROLE: Principal Actuarial Systems Architect and Financial Governance Auditor.

CONTEXT:
You are reviewing a structured JSON payload generated by an automated .NET spreadsheet parser. This payload represents a highly technical life/pension insurance workbook derived from the 'edu-2012-c13-01' actuarial engine matrix (covering GAAP reserves, Universal Life roll-forwards, variable payout mechanics, and stochastic projections).

Your objective is to reverse-engineer these raw math vectors into an institution-grade, plain-English Business Rules Specification document. You are NOT compiling code; you are extracting human-auditable financial logic and identifying system risks.

CRITICAL TASK WORKFLOW:

1. CONCEPTUAL IDENTIFICATION & ALIGNMENT
   Analyze the 'ClassificationArchetype' and 'CompressedVectors' shapes. Identify the precise textbook actuarial framework being utilized. You must explicitly name the underlying methodologies using standard nomenclature (e.g., Fackler's Cumulative Reserve Accumulation, Prospective Whole Life Reserve mechanics, DAC Cost Amortization via Gross Profit margins, or Assumed Investment Rate asset depletion curves).

2. SEMANTIC VARIABLE RESOLUTION
   Translate abstract positional coordinates (e.g., GlobalAnchors, Column letters) into clear business variables based on context metadata.
   - Example translation: Map 'Inputs!$B$3' to 'Technical Valuation Discount Rate (i)'
   - Example translation: Map 'Table 13.4 Col N' to 'Universal Life Fund Value End of Period (AV_t)'

3. WORD-BASED ALGORITHMIC SPECIFICATION
   Write a step-by-step, chronological description of how data streams through this worksheet model. You must use clear, financial-intent language instead of Excel syntax.
   - DO NOT write: "VLOOKUP the age in table 15.10 and multiply by cell E4."
   - DO write: "Interpolate the attained age against the designated mortality table matrix to extract the current period death decrement baseline probability (q_x+t). Apply this probability to the policy net amount at risk to calculate the period cost of insurance loading."

4. MODEL AUDIT & GOVERNANCE RISK LOG
   Actively scan the data payload for implicit structural flaws, operational vulnerabilities, and regulatory model risks. Specifically look for and flag:
   - Hardcoded values buried inside dynamic vector formula matrices (e.g., static premium tax percentages or maintenance charges).
   - Abrupt boundary conditions (e.g., how the calculation logic handles the terminal age of a mortality lookup table or zero-percent interest rate floors).
   - Lack of sensitivity margins or provisions for adverse deviation (PAD) within reserve lines.
   - Broken cell exceptions or unresolved string flags passed from the parser layer.

OUTPUT STRUCTURE:
Your output must conform strictly to this markdown header structure:
# Actuarial Function Specification: [Insert Target Sheet Title]
## 1. Executive Summary & Core Framework
## 2. Business Variable Dictionary
## 3. Chronological Step-by-Step Logic Flow
## 4. Model Governance & Operational Risk Log

CONSTRAINTS:
- Do not output generic developer descriptions. Speak native actuarial terminology.
- Do not output Python, C#, or pseudo-code blocks in Section 3. Use pure, structured semantic narrative.

```

---

## 6. Automated Reconciliation Spec (Roslyn Dynamic Compilation Contract)

To implement the validation loop in the .NET application core, the LLM must output its mathematical ruleset as a clean, self-contained C# stateless class implementing a standardized interface (`IActuarialReconciliationUnit`). The .NET platform core will ingest this generated code string at runtime, compile it dynamically using Microsoft.CodeAnalysis.CSharp (Roslyn), and run the scalar inputs through the natively compiled assembly.

```
+--------------------------------------------------------------+
|                    Reconciliation Script                     |
+--------------------------------------------------------------+
                               |
            [1] Read In-Force / Input Scalar Values
                               |
                               v
+--------------------------------------------------------------+
|             Roslyn Dynamic Compilation Contract              |
|   (Compiles LLM C# IActuarialReconciliationUnit string)      |
+--------------------------------------------------------------+
                               |
                  [2] Output Calculated Value
                               |
                               v
+--------------------------------------------------------------+
|                     Verification Phase                       |
|   (Compares Output against Sheet Evaluated Value via Delta)  |
+--------------------------------------------------------------+
                               |
         +---------------------+---------------------+
         |                                           |
         v                                           v
  [Variance == 0]                             [Variance != 0]
         |                                           |
         v                                           v
[Certify Spec Verified]                    [Flag Logic Leak Error]

```

### 1. Verification Test Bed Mapping

For a specific target sheet block (e.g., `Example 18.4 - Whole Life GAAP Reserves`), the verification script isolates a control testing slice (e.g., Row 12, Attained Age 91).

### 2. Scalar Input Injection

The validation framework extracts the base scalars for that specific slice out of the parsed Excel database record:

* `In Force BOY` = $0.23124$
* `GAAP Interest Rate` = $0.055$
* `GAAP Mortality with PAD` = $0.15824$

### 3. Execution Pass

The script dynamically compiles the LLM-generated C# logic into memory using Roslyn, loads it via an isolated `AssemblyLoadContext`, and runs the scalar inputs directly through the natively compiled dynamic assembly.

### 4. Variance Tolerance Testing

The script compares the generated rule calculation against the physical `.EvaluatedValue` captured from cell `Example 18.4!AF12`.


$$\text{Variance Delta} = \left| \text{Rule Output Value} - \text{Excel Stated Value} \right|$$

* **Rule A (Success Pass):** If $\text{Variance Delta} \le 0.00001$, stamp the tracking record as **Mathematically Certified**.
* **Rule B (Failure/Leak Pass):** If $\text{Variance Delta} > 0.00001$, flag a **Logic Leak Error**. Lock out the generation pipeline, isolate the target structural variable column containing the deviation, and push the cell context back to the LLM agent for auto-correction iteration cycles.

---

## 7. Exception Handling Core (VBA, Volatile Functions, Formula Anomalies)

This module defines how the .NET extraction layer identifies, sanitizes, and logs non-standard, uncomputable, or volatile spreadsheet behaviors before generating the context payload for the LLM Interrogation Core.

### 1. Exception Categorization Matrix

When scanning sheets like `Example 16.3` (which relies on random simulations) or sheets containing broken legacy references, the `ActuarialExtractionEngine` must catch and tag spreadsheet anomalies using an explicit .NET enumeration model:

```csharp
public enum ActuarialNodeExceptionType
{
    None,
    ExcelNativeError,     // #REF!, #VALUE!, #DIV/0!, #NAME?, #NUM!
    VolatileFunction,     // RAND(), NOW(), OFFSET(), INDIRECT()
    CircularReference,    // Intentional or accidental iterative loops
    ExternalWorkbookLink, // Reference to local network drives or missing sheets
    VBAMacroDependency    // Values written/altered via underlying macro events
}
```

### 2. Deterministic Handling Protocols (.NET Implementation)

Instead of passing raw failure blocks, the .NET layer sanitizes the data stream according to the following strict algorithmic boundaries:

*   **Protocol A: Excel Native Errors (#REF!, #VALUE!, etc.)**
    *   *The Vulnerability:* Downstream LLMs will attempt to hallucinate what the math should be if handed a raw error coordinate.
    *   *The Handling Rule:* The .NET parser intercepts the error token via XLParser. It immediately captures the raw text of the broken formula but overwrites the EvaluatedValue with an explicit diagnostic tag: `<ERROR_STATE: [ErrorType]>`. It truncates the dependency lineage pass at this node to prevent error cascading through the JSON graph.
*   **Protocol B: Volatile Functions (RAND(), OFFSET())**
    *   *Target Match:* `Example 16.3 - Part 1` (Stochastic Generation of Fund Values tracking "Random Number" distributions).
    *   *The Vulnerability:* Volatile cells shift values on every workbook open event, corrupting the deterministic validation loops.
    *   *The Handling Rule:* The parser flags the presence of dynamic seeds. It hard-locks the SampleEvaluatedValue to the specific static value frozen in the file at the moment of ingestion. It injects a metadata flag `IsVolatile = true` into the JSON schema, instructing the LLM to write a functional simulation wrapper rather than a static linear rule.
*   **Protocol C: External Workbook Links ([Legacy_Book.xlsx]Tab1!A1)**
    *   *The Vulnerability:* The .NET compiler cannot reach external environments to extract the calculation path.
    *   *The Handling Rule:* The AST parser isolates the external file token. It extracts the last-cached value stored locally in the XML layer, wraps the node in a boundary token (`<EXTERNAL_BOUNDARY>`), and catalogs the filename as a structural global dependency.

### 3. Updated JSON Schema Segment (DisruptiveNodes Payload)

When an anomaly matches any category in the Matrix, the node is completely isolated and appended to the `DisruptiveNodes` array within the unified parser data contract:

```json
{
  "Coordinate": "Example 16.3!B7",
  "RawFormula": "=RAND()",
  "EvaluatedValue": "0.1431813998",
  "ExceptionFlag": "VolatileFunction",
  "Telemetry": {
    "ImpactedDownstreamCells": ["Example 16.3!C7", "Example 16.3!D7"],
    "IsStochasticSeed": true,
    "MitigationInstruction": "Treat value as an invariant snapshot seed for verification, but document logic as a probabilistic random variable distribution step."
  }
}
```

### 4. LLM Interrogation Guardrails (Prompt Extensions)

To ensure the LLM handles these exceptions gracefully, the following operational instructions are automatically injected into the Model Audit & Governance Risk Log block of the Master System Prompt whenever `DisruptiveNodes.Count > 0`:

```text
CRITICAL EXCEPTION HANDLING AMENDMENTS:
Your provided payload contains explicit [DisruptiveNodes] flags. You must expand 'Section 4: Model Governance & Operational Risk Log' to include a dedicated 'Spreadsheet Telemetry Breakdown':

1. For every node flagged as 'ExcelNativeError', explicitly write an enterprise Jira-style engineering ticket within the markdown detailing the exact coordinate, the broken formula string, and the remediation path required by a data engineer to clean the source file.
2. For every node flagged as 'VolatileFunction' (e.g., Stochastic inputs found in Example 16.3), analyze the lookup logic arrays horizontally. Define the exact boundary conditions of the distribution curve (e.g., tracking the probability buckets and matching dynamic return scales) and explicitly state that the system cannot translate this row as a pure deterministic formula.
3. For every node flagged as 'ExternalWorkbookLink', log the missing system dependency explicitly. State the structural impact of the missing data profile on the calculation path of the primary target asset line.
```

### 5. Reconciliation Loop Modification for Anomalies

When the dynamic Roslyn verification loop executes against a sheet containing a DisruptiveNode, the system modifies its test boundaries:

*   **If the node is an ExcelNativeError or ExternalWorkbookLink:** The ReconciliationEngine automatically suspends penny-perfect validation for that specific lineage tree branch. It logs a status of `Inconclusive_Source_Corrupted` and skips to the next clean calculation partition.
*   **If the node is a VolatileFunction:** The system overrides dynamic execution. It manually overrides the output of the dynamically compiled Roslyn method with the frozen EvaluatedValue passed in the JSON metadata token (0.1431813998) to test if the downstream calculation chain matches the original spreadsheet's logic perfectly.

---

## 8. Persistence & Storage Layer (The Hybrid Database Metadata Pattern)

To persist the translated rules and bypass the deployment overhead of pure compiled code (Microservices), the platform utilizes **The Hybrid Database Metadata Pattern** backed by an embedded SQLite database.

### 1. The Entity Framework Core (EF Core) Abstraction

The persistence layer uses EF Core 8+ (`Microsoft.EntityFrameworkCore.Sqlite`) to maintain standard relational table structures. SQLite is utilized as the local storage medium because it forces a strict SQL paradigm, allowing for a single-line "lift-and-shift" migration to an enterprise PostgreSQL or SQL Server cluster in the future simply by swapping the EF Core provider.

### 2. JSONB Target Payload Storage

While the metadata is relational (utilizing a strict 3-tier Guid hierarchy: `ActuarialWorkbook` $\rightarrow$ `WorksheetTopology` $\rightarrow$ `RuleTranslationVersion`), the actual rule payloads are completely schema-less. 

The core output of the LLM Interrogation and Reconciliation loops is serialized into a single `TranslationPayload` JSON object containing:
1. **The Auditable Markdown Spec:** The human-readable actuarial rule explanation.
2. **The Stateless C# Source Code:** The raw, `IActuarialReconciliationUnit` compliant C# string.

In SQLite 3.45.0+, EF Core maps these complex JSON payloads natively using the `.ToJson()` Fluent API, isolating the unstructured data within a `jsonb()` compatible column while maintaining relational auditing externally.

### 3. Dynamic Runtime Loading

When a rule needs to be executed in production (or evaluated in a UI dashboard), the WebAPI executes a fast sub-millisecond query to pull the active code:

```csharp
public async Task<decimal> ExecuteActiveRuleAsync(Guid worksheetId, Dictionary<string, decimal> rowInputs)
{
    // 1. Fetch the active translation payload natively deserialized by EF Core
    var activeRule = await _dbContext.RuleTranslations
        .Where(rt => rt.WorksheetId == worksheetId && rt.IsActive == true)
        .Select(rt => rt.Payload.GeneratedCSharpMirrorCode)
        .SingleOrDefaultAsync();

    if (string.IsNullOrEmpty(activeRule))
        throw new ApplicationException("No active actuarial rule found for this worksheet.");

    // 2. Pass the retrieved C# string directly to the Phase III-B Roslyn Compiler
    return _roslynEngine.CompileAndExecute(activeRule, rowInputs);
}
```

This architecture allows actuaries to update the underlying logic (regenerated by the LLM) and save it back to the database as data, instantly hot-reloading the business logic at runtime without triggering a CI/CD code freeze.

---

## 9. Runtime Safety & Verification Architecture (Phase III-B Risk Mitigations)

The decision to dynamically compile and execute LLM-generated C# code at runtime creates a deliberate Remote Code Execution (RCE) pipeline. The following architectural controls are **mandatory** and must be implemented and unit-tested before the first live LLM call is executed.

> [!CAUTION]
> **Gate Condition:** The AST Safety Scanner (§9.1) and the Execution Timeout (§9.2) MUST be implemented in the `ActuarialTranslationEngine.Engine` project and pass unit tests before the `LiveDomainInterrogationBridge` is wired into the CLI.

### 1. AST Safety Scanner (RISK-S1 Mitigation)

**The Vulnerability:** Passing unchecked text from an LLM directly into a native host compiler invites system compromise. The LLM could generate `System.IO.File.Delete()`, `Process.Start("cmd.exe")`, network exfiltration calls, or reflection-based assembly injection.

**The Architectural Requirement:** Before Roslyn compiles the LLM's C# output, a `CSharpSyntaxWalker`-based scanner must inspect the generated `SyntaxTree` and **immediately reject** any code that:

* Contains `using` directives beyond `System` and `System.Collections.Generic`.
* References any type in `System.IO`, `System.Net`, `System.Diagnostics`, `System.Reflection`, or `System.Runtime.InteropServices`.
* Instantiates any object outside the allowed set (`Dictionary<,>`, `List<>`, and primitive wrappers).
* Contains `static` method calls on types outside the allowed set.

Additionally, the Roslyn `MetadataReference` list passed to `CSharpCompilation.Create()` must be restricted to the minimum required assemblies. Dangerous assemblies (`System.IO.dll`, `System.Net.Http.dll`) must **never** be passed as references.

```csharp
public class AstSafetyScanner : CSharpSyntaxWalker
{
    private static readonly HashSet<string> ForbiddenNamespaces = new()
    {
        "System.IO", "System.Net", "System.Diagnostics",
        "System.Reflection", "System.Runtime.InteropServices",
        "System.Threading" // Prevents Thread.Sleep / infinite spin-waits
    };

    private readonly List<string> _violations = new();

    public IReadOnlyList<string> Violations => _violations;

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        string ns = node.Name?.ToString() ?? "";
        if (ns != "System" && ns != "System.Collections.Generic")
            _violations.Add($"Forbidden using directive: {ns}");
        base.VisitUsingDirective(node);
    }

    // Additional overrides for VisitInvocationExpression, VisitObjectCreationExpression, etc.
}
```

### 2. Execution Timeout (RISK-O1 Mitigation)

**The Vulnerability:** The LLM generates code containing `while(true)`, deeply recursive methods, or `Thread.Sleep(Timeout.Infinite)`. The host process hangs indefinitely. In Phase IV, a single request could starve the thread pool and bring down the entire API.

**The Architectural Requirement:** The `ExecuteCalculationRow()` invocation must be wrapped in a `Task.Run()` with a `CancellationTokenSource` and a hard timeout of **5 seconds**.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
decimal result;
try
{
    result = await Task.Run(() => instance.ExecuteCalculationRow(rowInputs), cts.Token);
}
catch (OperationCanceledException)
{
    throw new ActuarialDynamicCompilationException(
        "LLM-generated code exceeded the 5-second execution timeout. " +
        "Possible infinite loop or unbounded recursion detected.");
}
```

### 3. Multi-Row Sampling (RISK-A1 Mitigation)

**The Vulnerability:** The previous specification stated that testing one representative row per partition was sufficient because the formula structure was proven identical. However, an LLM that fails to understand the actuarial math can take the lazy route and **literally hardcode the output value** it sees in the payload (e.g., `return 953.6558m;`). Testing one row would result in a false "Pass", certifying a mathematically broken model.

**The Architectural Requirement:** The reconciliation loop must pull **three distinct verification scalar sets** from the Excel database for each `VectorRangePartition`:

| Sample Point | Row Selection | Purpose |
|-------------|---------------|---------|
| **First Row** | `StartRow` (or `StartRow + 1` if seed row) | Catches initialization boundary errors |
| **Mid-Point Row** | `(StartRow + EndRow) / 2` | Validates the general recurrence rule |
| **Last Row** | `EndRow` | Catches terminal boundary errors (e.g., mortality table end-of-life) |

The dynamically compiled C# class must calculate **all three correctly** within the $\le 0.00001$ variance ceiling. This mathematically proves the LLM generated a generalized algorithmic rule, not a hardcoded scalar.

> [!IMPORTANT]
> **Structural Error Suppression applies per-sample:** If any of the three selected rows references a cell flagged in the `DisruptiveNodes` collection, the engine must step to the next structurally clean row in that direction (forward for First/Mid, backward for Last).

### 4. Collectible Assembly Contexts (RISK-O2 Mitigation — Architect Override)

**The Vulnerability:** The previous specification (§6 of this blueprint and `enterprise-lifecycle-spec.md` Phase III-B) instructed the agent to use `isCollectible: false` for the `AssemblyLoadContext` during Phase III-B, deferring the switch to `true` until Phase IV. This creates an immediate memory leak: processing 45 sheets means 45 un-collectible assemblies permanently locked in RAM.

**The Architectural Override:** `isCollectible: true` wrapped in a `using` scope must be implemented **immediately at the start of Phase III-B**, not deferred to Phase IV. The previous instruction is hereby overturned.

```csharp
// CORRECTED: isCollectible is true from Phase III-B onwards
using var isolatedContext = new AssemblyLoadContext("ActuarialValidationScope", isCollectible: true);

ms.Position = 0;
Assembly assembly = isolatedContext.LoadFromStream(ms);

var type = assembly.GetType("DynamicReconciliationUnit");
var instance = (IActuarialReconciliationUnit)Activator.CreateInstance(type)!;

decimal ruleCalculatedResult = instance.ExecuteCalculationRow(rowInputs);

// Context is automatically unloaded here via the using scope,
// reclaiming all memory allocated by the dynamic assembly.
```

### 5. Risk Mitigation Summary Matrix

| Risk ID | Risk | Severity | Mitigation | Status |
|---------|------|----------|------------|--------|
| RISK-S1 | Arbitrary Code Execution | 🔴 Critical | AST Safety Scanner (§9.1) | **MANDATORY — Gate Condition** |
| RISK-O1 | Infinite Loops / Hangs | 🟠 High | 5-second Execution Timeout (§9.2) | **MANDATORY — Gate Condition** |
| RISK-A1 | Silent Math Incorrectness | 🔴 Critical | Multi-Row Sampling — 3 rows per partition (§9.3) | **MANDATORY** |
| RISK-O2 | Memory Leaks | 🟡 Medium | `isCollectible: true` from Phase III-B (§9.4) | **Architect Override Applied** |
| RISK-S2 | Prompt Injection via Excel | 🟠 High | AST Scanner (defence in depth) + input sanitisation | Phase IV |
| RISK-A2 | Non-Reproducibility | 🟡 Medium | Model version pinning + hash verification | Phase III-B (pin), IV (persist) |
| RISK-R1 | Tight Coupling to Provider | 🟡 Medium | Abstract response parser interface | Phase IV |

> **Cross-Reference:** The full risk analysis with detailed threat modelling is documented in [Phase3B_Risk_Analysis.md](docs/Phase3B_Risk_Analysis.md).
