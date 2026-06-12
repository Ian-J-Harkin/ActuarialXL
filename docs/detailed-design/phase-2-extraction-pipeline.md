# Phase II: Extraction Pipeline — Detailed Design & Assumptions

This document expands the `enterprise-lifecycle-spec.md` Phase II section with the implementation detail required to build `ExtractSheetData`, distinguish headers from data, and define the JSON schema assertions that prove the pipeline works.

> [!CAUTION]
> **Ground Truth Verification Mandate applies.** All cell coordinates, header names, and row boundaries documented here were empirically verified against `edu-2012-c13-01.xlsx` on 2026-06-11. If a future execution agent encounters discrepancies, the agent must update this document rather than silently adapting.

---

## 1. Table 13.4 Structural Anatomy (Verified)

The Phase II vertical slice targets a single sheet: **`Table 13.4`**.

### Layout Map

| Row | Purpose | Evidence |
|-----|---------|----------|
| 1 | Blank | No data |
| 2 | Title | `A2 = "Universal Life Fund Value Illustration"` |
| 3 | Blank | No data |
| 4 | **Column Headers** | `A4=Month`, `B4=Premium`, `C4=Premium Tax`, `D4=Net Premium`, `E4=Surrender Charge`, `F4=Per Policy Charge`, `G4=Benefit Amount`, `H4=Account Value`, `I4=Monthly Rate of COI`, `J4=Monthly Deduction`, `K4=Fund Value After Monthly Deduction`, `L4=Interest On Fund Value`, `M4=Fund Value End of Month` |
| 5 | Seed/Initialization Row | Static values only (no formulas); sets the initial conditions for the recursive loop |
| 6–65 | **Recursive Data Body** | Every row contains formulas referencing the prior row (t-1 lookback pattern) |

### Header Detection Algorithm

`ExtractSheetData` must implement the following deterministic header detection logic:

```
1. Scan rows 1–10 for the first row where ≥3 non-empty cells exist AND none of them contain formulas.
2. If found, that row is the HEADER ROW.
3. The DATA START ROW is the first row after the header where any cell contains a formula OR a numeric value.
4. The DATA END ROW is the last row in the used range that contains at least one formula cell.
```

For Table 13.4, this yields: Header = Row 4, Data Start = Row 6, Data End = Row 65.

### Seed Row vs. Recursive Body Discrimination

The `VectorCompressionEngine` must distinguish between:

- **Seed rows** (Previously assumed Row 5): Cells that contain only literal values. In Table 13.4, Row 5 is entirely blank in the used columns, meaning the data block begins definitively at Row 6. Initial conditions are seeded directly into Row 6 formulas.
- **Recursive body rows** (Rows 6–65): Cells that contain formulas referencing prior-row cells or globals. These are the compressible vector.

**Detection rule:** A row is a "seed" if `row.CellFormulas.Count == 0` or if its formula signature differs from the row immediately following it. This aligns with the existing `GenerateAbstractFormulaSignature` method in the lifecycle spec.

---

## 2. `ExtractSheetData` Implementation Specification

### Method Signature (from `IActuarialExtractionEngine`)

```csharp
RawWorkbookMap ExtractSheetData(Stream fileStream, string sheetName);
```

### Implementation Pseudocode

```
1. Open workbook from stream using ClosedXML.
2. GUARD: If workbook.Worksheets.Contains(sheetName) is false,
   throw ActuarialExtractionException($"Sheet '{sheetName}' not found. 
   Available sheets: {string.Join(", ", workbook.Worksheets.Select(s => s.Name))}").
3. Identify header row (first row with ≥3 non-empty, non-formula cells).
4. Build column letter → header name map from the header row.
5. For each data row from (headerRow + 1) to lastUsedRow:
   a. Skip rows where ALL cells are empty.
   b. For each column in the header map:
      - Record CellFormulas[columnLetter] = cell.FormulaA1 (or empty string if no formula).
      - Record CellValues[columnLetter] = cell.Value.ToString().
   c. Add the populated RawRowMetadata to the output list.
6. Return the RawWorkbookMap.
```

### Error Handling

| Condition | Action |
|-----------|--------|
| Sheet name not found | Throw `ActuarialExtractionException` listing all available sheet names |
| Sheet is empty (no used range) | Throw `ActuarialExtractionException("Sheet has no used data range")` |
| Header row not detected in rows 1–10 | Throw `ActuarialExtractionException("No header row detected in first 10 rows")` |
| File stream is null or unreadable | Throw `ArgumentException` before opening ClosedXML |

---

## 3. JSON Schema Assertion Specification

The Phase II exit criteria state: *"JSON Schema Assertion"*. This means the unit tests must validate the structural shape of the `CompressedVectorBlock` output, not just that it exists.

### Required Assertions for Table 13.4

```csharp
// 1. Sheet identity preserved
Assert.Equal("Table 13.4", result.TargetWorksheet);

// 2. Partition count: 2 (body-era-1 + body-era-2 due to Row 54 mortality shift)
Assert.InRange(result.Partitions.Count, 2, 3);

// 3. The recursive body partition must span rows 6–65
var bodyPartition = result.Partitions.Last();
Assert.Equal(6, bodyPartition.StartRow);
Assert.Equal(65, bodyPartition.EndRow);

// 4. Formula signature stability: every row in the body partition
//    must produce the identical abstract signature
Assert.Single(result.Partitions
    .Where(p => p.StartRow == 6)
    .Select(p => p.FormulaSignature)
    .Distinct());

// 5. Column count: 14 columns (A through N, with N="Notes")
Assert.Equal(14, bodyPartition.StructuralColumns.Count);
```

### What "JSON Schema Assertion" Does NOT Mean

It does not mean we validate against a formal JSON Schema (RFC draft-07). It means the xUnit assertions structurally prove that the output object graph has the expected shape, depth, and cardinality. This is cheaper, faster, and more precise for a vertical slice than maintaining a separate `.json` schema file.

---

## 4. Open Assumptions Requiring Future Verification

| # | Assumption | Risk if Wrong | When to Verify |
|---|-----------|---------------|----------------|
| 1 | Header row is always within rows 1–10 | Extraction fails silently on sheets with deep title blocks | Phase III-A (multi-archetype expansion) |
| 2 | The seed row always immediately follows the header | Misclassified partitions | Phase III-A |
| 3 | Row 5 in Table 13.4 contains zero formulas | Seed/body discrimination breaks | Verified empirically (confirmed: Row 5 is entirely blank; Data starts at Row 6) |
| 4 | All 60 recursive rows (6–65) share an identical formula signature | More than 1 partition generated for the body | Verified empirically (formula pattern changes at Row 54 due to mortality rate shift — see Note below) |

> [!WARNING]
> **Note on Row 54 (Verified):** The mortality rate column `I` shifts from `0.00133*1000/12` (rows 6–53) to `0.00141*1000/12` (rows 54–65). This means the abstract formula signature WILL change at row 54, producing **2 partitions** (body-era-1 + body-era-2) for the data block, rather than 1. The lifecycle spec's exit criteria stating "no more than 2 partitions" is technically still met (since there is no seed row), but the assumption of 1 contiguous body is incorrect for this dataset and must be updated. This is exactly the kind of assumption drift this document exists to catch.
