using System;
using ClosedXML.Excel;

var wb = new XLWorkbook(@"c:\Github\ActuarialXLpoc\edu-2012-c13-01.xlsx");

foreach (var sheet in wb.Worksheets)
{
    Console.WriteLine($"\n=== SHEET: {sheet.Name} ===");
    
    // Find used range
    var usedRange = sheet.RangeUsed();
    if (usedRange == null) { Console.WriteLine("  (empty)"); continue; }
    
    int lastRow = usedRange.LastRow().RowNumber();
    int lastCol = usedRange.LastColumn().ColumnNumber();
    Console.WriteLine($"  Used Range: R1C1 to R{lastRow}C{lastCol}");
    
    // Print first 5 rows to understand header structure
    for (int r = 1; r <= Math.Min(6, lastRow); r++)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (int c = 1; c <= Math.Min(lastCol, 16); c++)
        {
            var cell = sheet.Cell(r, c);
            string val = cell.Value.ToString();
            if (!string.IsNullOrWhiteSpace(val))
            {
                string formula = cell.HasFormula ? $" [F:{cell.FormulaA1}]" : "";
                parts.Add($"{cell.Address}={val}{formula}");
            }
        }
        if (parts.Count > 0)
            Console.WriteLine($"  Row {r}: {string.Join(" | ", parts)}");
    }
    
    // Count formula cells and distinct formula signatures
    int formulaCells = 0;
    var signatures = new System.Collections.Generic.HashSet<string>();
    foreach (var cell in sheet.CellsUsed())
    {
        if (cell.HasFormula)
        {
            formulaCells++;
            // Normalize row numbers out of formula to get signature
            string sig = System.Text.RegularExpressions.Regex.Replace(cell.FormulaA1, @"\d+", "N");
            signatures.Add(cell.Address.ColumnLetter + ":" + sig);
        }
    }
    Console.WriteLine($"  Formula Cells: {formulaCells}, Distinct Column Signatures: {signatures.Count}");
}