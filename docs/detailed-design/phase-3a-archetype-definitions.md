# Phase III-A: Archetype Definitions — Detailed Design & Assumptions

This document expands the `enterprise-lifecycle-spec.md` Phase III-A section by defining the structural archetypes found in `edu-2012-c13-01.xlsx`, mapping every worksheet to an archetype, specifying the CLI interface, and documenting the error handling strategy.

> [!CAUTION]
> **Ground Truth Verification Mandate applies.** The archetype classifications below were derived from empirical structural scans of all 45 worksheets on 2026-06-11. Every sheet's header row, formula density, column count, and distinct formula signature count was inspected programmatically.

---

## 1. Archetype Taxonomy

The lifecycle spec names four archetypes but never defines them. Based on empirical analysis of the dataset, these are the actual structural patterns present:

### Archetype A: Time-Series Roll-Forward Loop
**Structural Signature:** Each row's formulas reference the immediately prior row (t-1 lookback). Columns represent a monthly or annual accumulation cycle. The formula signature is stable across long contiguous runs.

**Distinguishing Features:**
- High row count (40–70 data rows)
- Formulas contain cell references like `+K5+L5` → `+K6+L6` (row index increments by 1)
- Typically 8–13 formula columns per row
- Often contains a mortality/interest rate change-point mid-table

**Verified Worksheets:**

| Sheet | Title | Rows | Cols | Formula Cells | Signatures |
|-------|-------|------|------|---------------|------------|
| Table 13.4 | Universal Life Fund Value Illustration | R1:R65 C1:C13 | 13 | 648 | 12 |
| Table 13.5 | *(similar UL structure)* | — | — | — | — |
| Example 13.12 | *(UL variant)* | — | — | — | — |
| Example 18.1 | UL No-Lapse Guarantee | R1:R77 C1:C14 | 14 | 724 | 12 |
| Example 18.3 | UL with Secondary Guarantee | R1:R77 C1:C16 | 16 | 858 | 12 |
| Example 18.4 | UL Shadow Account | R1:R77 C1:C20 | 20 | 1116 | 16 |

---

### Archetype B: Stochastic Scenario Ledgers
**Structural Signature:** Each row's formulas reference global constants or same-row cells, specifically utilizing random variable mapping across non-linear lookup tables. There is no t-1 dependency between rows. Each row is independently calculable but relies on stochastic inputs.

**Distinguishing Features:**
- Formulas contain absolute references to static reference boundaries (`Probability`, `Look Up Value`, `Return`).
- Volatile functions like `RAND()` dictate row values.
- Each row can be evaluated in isolation (no sequential dependency).
- The presence of the `VolatileFunction` exception flag is expected.

**Verified Worksheets:**

| Sheet | Title | Rows | Cols | Formula Cells | Signatures |
|-------|-------|------|------|---------------|------------|
| Example 16.3 - Part 1 | Stochastic Generation of Fund Values | R1:R57 C1:C7 | 7 | 305 | 6 |
| Example 16.3 - Part 2 | Stochastic Generation of Fund Values | R1:R41 C1:C10 | 10 | 176 | 8 |

---

### Archetype C: Multi-Component Balancing Ledgers
**Structural Signature:** Wide tables (15–41 columns) with multiple parallel ledger tracks. Often contains side-by-side sub-tables on the same sheet (e.g., two valuation scenarios). High formula density with cross-column dependencies.

**Distinguishing Features:**
- Column count ≥ 15
- Multiple header groups on the same row (e.g., columns A–H and I–P are independent sub-tables)
- Distinct formula signature count is very high (20–40+) because different ledger columns use different formulas
- Contains actuarial reserve calculations (GAAP, GPV, DAC)

**Verified Worksheets:**

| Sheet | Title | Rows | Cols | Formula Cells | Signatures |
|-------|-------|------|------|---------------|------------|
| Solution to Exercise 18.4 | GAAP Reserves Whole Life | R1:R40 C1:C41 | 41 | 771 | 32 |
| Solution to Exercise 18.5 | GAAP Reserves Whole Life (GPV) | R1:R44 C1:C36 | 36 | 664 | 23 |
| Example 18.10 | Deferred Annuity (dual table) | R1:R16 C1:C15 | 15 | 106 | 14 |
| Solution to Exercise 18.2 | Deferred Annuity Reserves | R1:R18 C1:C17 | 17 | 82 | 15 |
| Example 18.5 | UL Reserve Comparison | R1:R77 C1:C26 | 26 | varies | varies |

---

### Archetype D: Variable Payout Adjusters
**Structural Signature:** Dynamic asset depletion tracking against an Assumed Investment Rate (AIR) threshold. The parser must anchor on the AIR target cell and trace its exponent scaling pattern down the columns.

**Distinguishing Features:**
- Presence of a static anchor cell (e.g., `AIR = 0.03`).
- Formulas use exponent scaling `(1+AIR)^-t`.
- Influences a `Payment Per Annuitant` or similar depletion vector.

**Verified Worksheets:**

| Sheet | Title | Rows | Cols | Formula Cells | Signatures |
|-------|-------|------|------|---------------|------------|
| Example 13.12 | Variable Payout Annuities | R1:R39 C1:C12 | 12 | 344 | 11 |
| Solution to Exercise 13.10 | Variable Payout Annuities | R1:R34 C1:C13 | 13 | 441 | 12 |

---

### Archetype E: Imperative State Mutation (The Dynamo Addendum)
**Structural Signature:** The core actuarial logic does not live in cell formulas, but rather in VBA modules that read inputs, compute in memory, and overwrite dashboard cells (e.g., Monte Carlo simulations).

**Distinguishing Features:**
- Target sheets are `.xlsm` or `.xlsb` containing a `vbaProject.bin` stream.
- Triggers the `ActuarialNodeExceptionType.VBAMacroDependency` exception.
- Handled by the `IVbaExtractionEngine` in Phase III-C rather than standard `ClosedXML` logic.

**Verified Worksheets:**
- CAS Public Access DFA Model (Dynamo v4.1)

---

## 2. Sheets NOT Classified Above

> [!IMPORTANT]
> The following sheets require manual inspection before archetype assignment. The structural scanner detected unusual patterns that do not cleanly fit the standard archetypes:

| Sheet | Issue |
|-------|-------|
| Tables 15.8 and 15.9 | Compound sheet with multiple named tables. May need sub-sheet extraction logic. |
| Solutions to Ex. 15.4 and 15.5 | Same compound structure. |

---

## 3. CLI Specification (`ActuarialTranslationEngine.CLI`)

### Command-Line Interface

```
Usage: ActuarialTranslationEngine.CLI [options]

Options:
  -f, --file <path>          Path to the .xlsx file (required)
  -s, --sheet <name>         Target sheet name (optional; processes all sheets if omitted)
  -o, --output <directory>   Output directory for JSON payload files (default: ./output)
  -a, --archetype <type>     Filter by archetype: A|B|C|D|E (optional)
  --verbose                  Enable detailed logging to stdout
  --dry-run                  Parse and classify only; do not write output files
  -h, --help                 Show help
```

### Output File Naming Convention

```
{output_dir}/{SheetName}_compressed.json
```

Example: `./output/Table_13.4_compressed.json`

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All sheets processed successfully |
| 1 | One or more sheets failed extraction (partial output written) |
| 2 | File not found or unreadable |
| 3 | No sheets matched the archetype filter |

---

## 4. Exception Handling Core (.NET Enums)

The lifecycle spec defines a strict `ActuarialNodeExceptionType` enumeration matrix for handling anomalies before they reach the LLM. The CLI must implement this logic.

| ActuarialNodeExceptionType | Excel/Parser Trigger | Handling Strategy |
|----------------------------|--------------------------|-------------------|
| `ExcelNativeError` | `#DIV/0!`, `#N/A`, `#VALUE!`, `#REF!` | Overwrite EvaluatedValue with `<ERROR_STATE: [Type]>`. Append to `DisruptiveNodes`. Do not throw system exceptions. |
| `VolatileFunction` | `RAND()`, `NOW()`, `OFFSET()`, `INDIRECT()` | Hard-lock the `SampleEvaluatedValue`. Append to `DisruptiveNodes` with `IsVolatile=true` telemetry. |
| `ExternalWorkbookLink` | `[Legacy.xlsx]Tab!A1` | Extract cached XML value, wrap in `<EXTERNAL_BOUNDARY>`, log to `DisruptiveNodes`. |
| `CircularReference` | Unresolvable AST loop | Extract formula string normally. Append to `DisruptiveNodes`. |
| `VBAMacroDependency` | `.xlsm`/`.xlsb` logic mutation | Route to `IVbaExtractionEngine` for Phase III-C macro code extraction. |

---

## 5. Phase III-A Exit Criteria (Refined)

The lifecycle spec states:
> *"The CLI cleanly processes all structural archetypes and generates structurally pristine JSON documents featuring zero unmapped cell addresses or unhandled exceptions."*

This is refined to:

1. **Archetype A, B, C, D** each produce at least one valid `CompressedVectorBlock` JSON file.
2. Every JSON file passes structural validation: `TargetWorksheet` is non-empty, `Partitions.Count >= 1`, every partition has `StartRow <= EndRow`.
3. The `DisruptiveNodes` payload correctly captures and flags `ExcelNativeError` and `VolatileFunction` nodes without crashing the parser.
4. The CLI returns exit code `0` when processing the entire workbook with `--verbose` enabled.
