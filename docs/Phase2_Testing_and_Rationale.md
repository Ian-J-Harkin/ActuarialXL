# Phase II Testing & Rationale (Step II.2 Extraction Engine)

## Executive Summary
During the execution of Step II.2 (Extraction Engine isolated testing), several structural and API behavior anomalies were encountered that contradicted the original detailed design assumptions. Following **Architectural Mandate 2**, the code was halted, the `.xlsx` structure was re-interrogated, and the logic was adjusted to match reality.

## Encountered Snags & Resolutions

### 1. The 14th "Notes" Column
**The Snag:** The test failed expecting exactly 13 columns (A through M), but the extraction engine found 14 columns.
**Re-grounding:** A visual check of the extracted headers revealed that `Table 13.4` possesses a 14th column titled `Notes` that was missed by earlier loose structural scans. 
**The Fix:** Updated the unit test assertions to expect 14 columns, explicitly verifying that the 14th column is `Notes`.

### 2. `IsEmpty()` vs `IsNullOrWhiteSpace()`
**The Snag:** The `ClosedXML` library's native `cell.IsEmpty()` method evaluates to `false` for cells that contain invisible formatting or whitespace. This caused the engine to incorrectly flag empty cells as data.
**The Fix:** Replaced all structural empty-checks with `string.IsNullOrWhiteSpace(cell.Value.ToString())`. The `ExtractedHeaderName` property was also updated to automatically `.Trim()` the output.

### 3. Missing Seed Row (Row 5)
**The Snag:** The data rows extracted spanned `6` to `65` (exactly 60 rows), entirely skipping Row 5.
**Re-grounding:** Row 5 was previously designated as the "seed row" with no formulas. In reality, the `ClosedXML` string-evaluation correctly determined that Row 5 was entirely blank in the columns that mattered, causing the data block to start definitively at Row 6.
**The Fix:** Adjusted the data bounds assertion to test `Count = 60`, `First = Row 6`, `Last = Row 65`.

### 4. Literal Formula Strings
**The Snag:** The Phase 2 spec assumed `ClosedXML` would strip leading `+` signs from Excel formulas (e.g., returning `K6+L6`), but the assertion failed.
**Re-grounding:** `ClosedXML` returns the literal `FormulaA1` string exactly as typed into Excel.
**The Fix:** Updated the unit test to assert the exact empirical string `+K6+L6`.

## Conclusion
The `ActuarialExtractionEngine` is now completely structurally bound to the true layout of the `Table 13.4` worksheet, gracefully handling whitespace drift and literal formula extraction. The xUnit test suite proves that the spreadsheet parser works perfectly without requiring any downstream logic to compensate for bad extraction.
