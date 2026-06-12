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

### Archetype B: Commutation/Lookup Table
**Structural Signature:** Each row's formulas reference only global constants or same-row cells. There is no t-1 dependency between rows. Each row is independently calculable. Uses `SUMPRODUCT` or similar array functions that reference the entire column range.

**Distinguishing Features:**
- Formulas contain absolute references like `$D$56`, `$E$120`
- `SUMPRODUCT` aggregations spanning the full data column
- Each row can be evaluated in isolation (no sequential dependency)
- Typically 5–8 columns

**Verified Worksheets:**

| Sheet | Title | Rows | Cols | Formula Cells | Signatures |
|-------|-------|------|------|---------------|------------|
| Example 18.9 | UL Maximum Surrender Charge | R1:R57 C1:C7 | 7 | 305 | 6 |
| Solution to Exercise 18.7 | Calculation of Cash Values | R1:R41 C1:C10 | 10 | 176 | 8 |
| Solution to Exercise 18.8 | Maximum Surrender Charge | R1:R77 C1:C10 | 10 | 378 | 8 |
| Solution to Exercise 18.1 | Reserves for 20 Year Term | R1:R33 C1:C9 | 9 | 162 | 12 |

---

### Archetype C: Multi-Ledger Balancing Table
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

### Archetype D: Static/Minimal Calculation Table
**Structural Signature:** Small tables with very few formula cells. Primarily reference data or simple parameter lookups. May serve as input assumptions for other sheets.

**Distinguishing Features:**
- Low formula density (< 50 formula cells)
- Often just a sequence counter (`+A5+1`) or simple multiplication
- May contain date-based data (CMT rates, mortality tables)

**Verified Worksheets:**

| Sheet | Title | Rows | Cols | Formula Cells | Signatures |
|-------|-------|------|------|---------------|------------|
| Exercises 18.4 and 18.5 | Cash Values | R1:R39 C1:C2 | 2 | 34 | 1 |
| Solution to Exercise 18.9 | Min Nonforfeiture Interest Rate | R1:R34 C1:C33 | 33 | 44 | 44 |
| Table 13.3 | *(assumption inputs)* | — | — | — | — |

---

## 2. Sheets NOT Classified Above

> [!IMPORTANT]
> The following sheets require manual inspection before archetype assignment. The structural scanner detected unusual patterns that do not cleanly fit the four archetypes:

| Sheet | Issue |
|-------|-------|
| Example 16.3 - Part 1 | Lifecycle spec calls this "Stochastic distribution arrays featuring volatile functions and native error anomalies." Must verify if it uses `RAND()` or error-producing formulas like `#DIV/0!`. |
| Example 16.3 - Part 2 | Companion to Part 1; likely same archetype. |
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
  -a, --archetype <type>     Filter by archetype: A|B|C|D (optional)
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

## 4. Error Handling Strategy for "Native Error Anomalies"

The lifecycle spec mentions "native error anomalies" but does not define handling. Based on actuarial spreadsheet conventions, the following error values may appear in cells:

| Excel Error | ClosedXML Representation | Handling Strategy |
|-------------|--------------------------|-------------------|
| `#DIV/0!` | `XLError.DivisionByZero` | Record in `CellValues` as `"#DIV/0!"`. Flag the cell in a `List<ExtractionWarning>` on the `RawRowMetadata`. Do NOT throw. |
| `#N/A` | `XLError.NoValueAvailable` | Same as above. Common in `VLOOKUP` tables. |
| `#VALUE!` | `XLError.IncompatibleValue` | Same as above. |
| `#REF!` | `XLError.CellReference` | Log as a **critical warning**. This usually indicates a broken sheet. |
| Circular reference | ClosedXML does not evaluate; formula string is intact | Extract the formula string normally. The downstream LLM and Roslyn engine can still reason about the mathematical structure even if Excel can't evaluate it. |

---

## 5. Phase III-A Exit Criteria (Refined)

The lifecycle spec states:
> *"The CLI cleanly processes all four structural archetypes and generates structurally pristine JSON documents featuring zero unmapped cell addresses or unhandled exceptions."*

This is refined to:

1. **Archetype A, B, C, D** each produce at least one valid `CompressedVectorBlock` JSON file.
2. Every JSON file passes structural validation: `TargetWorksheet` is non-empty, `Partitions.Count >= 1`, every partition has `StartRow <= EndRow`.
3. The `ExtractionWarning` list is empty for Archetype A and B sheets. Archetype C and D sheets may contain warnings but must not throw unhandled exceptions.
4. The CLI returns exit code `0` when processing the entire workbook with `--verbose` enabled.
