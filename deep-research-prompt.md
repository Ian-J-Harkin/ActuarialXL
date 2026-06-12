# Deep Research & Architecture Prompt: LLM Spreadsheet Ingestion Architecture

**Role:** You are a Principal Cloud Architect and AI/ML Engineer specializing in large language model integration, deterministic data parsing, and token optimization within enterprise .NET ecosystems.

**Goal:** Provide an actionable, production-grade technical architecture specification and data pipeline design for serializing, parsing, and ingesting massive, legacy life/pension actuarial Excel workbooks into a token-efficient format optimized for LLM semantic translation and model risk governance.

**Context:** We are building a .NET-based enterprise platform. The target inputs are highly complex legacy actuarial models (e.g., SOA UAP Chapters 13–18 workbooks). These contain deeply nested multi-tab formulas, compounding projection loops down columns, cross-tab lookups (e.g., mortality tables), and volatile calculations. Our goal is not to execute or compile the sheet, but to output an auditable, human-readable Business Rules Specification. 

**Scale Constraints for the Architecture:**
- File Sizes: Up to 50MB.
- Structural Density: 50+ worksheets, 200,000+ active cells, thousands of repeating sequential rows representing longitudinal projections.

---

### Strict Deliverable Requirements:

#### 1. SOTA Parsing & Structural Compression (.NET Implementation)
- Define a concrete strategy for mapping a 2D grid into a 1D token stream without losing coordinate lineage or semantic tab context.
- Translate the core principles of Microsoft's 'SpreadsheetLLM' (e.g., SheetCompressor, anchoring, grid-to-text formatting) into explicit C# algorithmic steps.
- Provide the precise logic for a row-compression algorithm: How should the .NET parser identify repeating sequential calculation rows (e.g., a 40-year projection loop) and collapse them into a single "formula template block" before passing them to the LLM?

#### 2. Deterministic Dependency Graph Extraction
- Propose a concrete C# architecture utilizing high-performance open-source libraries (specifically analyze combinations of `ClosedXML` or `EPPlus` for data reading, paired with `XLParser` for parsing the formula Abstract Syntax Trees).
- Define the exact JSON schema that this .NET parser must output to represent the calculation graph (Target Cell -> Formula -> Evaluated Value -> Upstream Cell Dependencies).
- Detail how the system should handle uncomputable cells, custom VBA macros, or `#REF!` errors without allowing the downstream LLM to hallucinate the missing logic.

#### 3. Prompting & Interrogation Strategy
- Design a multi-stage prompting or multi-agent strategy (e.g., Phase 1: Structural Extraction, Phase 2: Semantic Translation, Phase 3: Governance & Edge Case Logging).
- Specify the exact system prompt guardrails needed to force the LLM to output pure actuarial nomenclature (e.g., identifying Fackler's Cumulative Reserve loops, Net Single Premiums, Commutation functions) rather than generic developer descriptions.

#### 4. The Validation & Reconciliation Loop
- Define the technical architecture for an automated verification loop. How can the .NET system dynamically test the plain-English rules or intermediate logic extracted by the LLM against the raw, evaluated cell values from the original Excel file to guarantee accuracy down to the penny?

#### 5. Concrete Architectural Blueprints
- Propose 2 distinct architectural patterns for this pipeline (e.g., a lean, pipeline-based Extraction-Transform-Prompt model vs. a Graph-RAG over a vector/graph database for massive sheets).
- Contrast these patterns strictly on: Token consumption overhead, memory footprint within a .NET environment, and overall processing latency.
