# Output Validation Report

## 1. Directory Contents Audit
We verified that the `output/` directory contains **45 files** in total, corresponding to the sheets in the `edu-2012-c13-01.xlsx` workbook:
- Examples: `Example 13.12`, `Example 14.1`, `Example 16.1`, `Example 16.3 - Part 1/2`, `Example 18.1` through `18.10`.
- Exercises: `Exercises 18.4 and 18.5`.
- Tables: `Table 13.3`, `Table 13.4`, `Table 13.5`, `Table 13.8`, `Table 13.9`, `Table 13.10`, `Table 15.1`, `Table 15.10`, `Tables 15.8 and 15.9`.
- Solutions: Various sheets prefixed with `Solution to Exercise` and `Solutions to Ex. 15.4 and 15.5`.

All sheets successfully compiled down to JSON files. No extraction exception occurred during the pipeline run.

---

## 2. Schema Validation
We verified that the output payload structure matches the `CompressedVectorBlock` schema:

| JSON Key | C# Type / Model Mapping | Status |
|----------|-------------------------|--------|
| `TargetWorksheet` | `string` | Valid |
| `ProcessingArchetype` | `string` | Valid (defaults to empty string) |
| `Partitions` | `List<VectorRangePartition>` | Valid |
| `Partitions[].StartRow` | `int` | Valid |
| `Partitions[].EndRow` | `int` | Valid |
| `Partitions[].FormulaSignature` | `string` | Valid |
| `Partitions[].StructuralColumns` | `List<ColumnDefinition>` | Valid |
| `Partitions[].StructuralColumns[].ColumnLetter` | `string` | Valid |
| `Partitions[].StructuralColumns[].ExtractedHeaderName` | `string` | Valid |
| `Partitions[].StructuralColumns[].TokenizedFormulaTemplate` | `string` | Valid |
| `Partitions[].StructuralColumns[].ChronologicalLookbacks` | `List<int>` | Valid |
| `DisruptiveNodes` | `List<DisruptiveNode>` | Valid |

### Sample Inspected File: `Exercises 18.4 and 18.5_compressed.json`
```json
{
  "TargetWorksheet": "Exercises 18.4 and 18.5",
  "ProcessingArchetype": "",
  "Partitions": [
    {
      "StartRow": 3,
      "EndRow": 5,
      "FormulaSignature": "A:",
      "StructuralColumns": [
        {
          "ColumnLetter": "A",
          "ExtractedHeaderName": "Cash Values for Exercises 18.4 and 18.5",
          "TokenizedFormulaTemplate": "",
          "ChronologicalLookbacks": []
        }
      ]
    },
    {
      "StartRow": 6,
      "EndRow": 39,
      "FormulaSignature": "A:\\u002BCol[A][-1]\\u002B1",
      "StructuralColumns": [
        {
          "ColumnLetter": "A",
          "ExtractedHeaderName": "Cash Values for Exercises 18.4 and 18.5",
          "TokenizedFormulaTemplate": "\\u002BCol[A][-1]\\u002B1",
          "ChronologicalLookbacks": [
            1
          ]
        }
      ]
    }
  ],
  "DisruptiveNodes": []
}
```

- **Row 3-5**: Contain header text/metadata without formulas (`FormulaSignature` is `"A:"`).
- **Row 6-39**: Contain chronological formulas looking back one step (`+Col[A][-1]+1`). The engine successfully tokenized this and resolved the lookup index `1` in `ChronologicalLookbacks`.

---

## 3. Findings & Conclusions
- **No data loss**: 45 sheets were expected, and 45 output files are present.
- **Accurate mapping**: The structural partitions align precisely with the `CompressedVectorBlock` definition.
- **Header correctness**: The extended 500-row limit in the `ActuarialExtractionEngine` allowed sheets with deep header rows (like `Exercises 18.4 and 18.5`) to be processed completely.

*Validated on 2026-06-15.*
