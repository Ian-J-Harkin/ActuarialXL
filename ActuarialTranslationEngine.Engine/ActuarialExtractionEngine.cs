using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ClosedXML.Excel;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Exceptions;

namespace ActuarialTranslationEngine.Engine;

public class ActuarialExtractionEngine : IActuarialExtractionEngine
{
    public RawWorkbookMap ExtractSheetData(Stream fileStream, string sheetName)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        
        using var workbook = new XLWorkbook(fileStream);
        
        if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
        {
            var available = string.Join(", ", workbook.Worksheets.Select(s => s.Name));
            throw new ActuarialExtractionException($"Sheet '{sheetName}' not found. Available sheets: {available}");
        }

        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
        {
            throw new ActuarialExtractionException("Sheet has no used data range");
        }

        int headerRowNumber = -1;
        var headers = new List<ColumnDefinition>();

        // 1. Find Header Row (rows 1-10)
        for (int r = 1; r <= Math.Min(10, usedRange.LastRowUsed().RowNumber()); r++)
        {
            var row = worksheet.Row(r);
            var cells = row.CellsUsed();
            
            int nonEmptyCount = 0;
            bool hasFormula = false;

            foreach (var cell in cells)
            {
                if (!string.IsNullOrWhiteSpace(cell.Value.ToString())) nonEmptyCount++;
                if (cell.HasFormula) hasFormula = true;
            }

            if (nonEmptyCount >= 3 && !hasFormula)
            {
                headerRowNumber = r;
                foreach (var cell in cells)
                {
                    if (string.IsNullOrWhiteSpace(cell.Value.ToString())) continue;
                    
                    headers.Add(new ColumnDefinition 
                    { 
                        ColumnLetter = cell.WorksheetColumn().ColumnLetter(), 
                        ExtractedHeaderName = cell.Value.ToString().Trim()
                    });
                }
                break;
            }
        }

        if (headerRowNumber == -1)
        {
            throw new ActuarialExtractionException("No header row detected in first 10 rows");
        }

        var map = new RawWorkbookMap
        {
            SheetName = sheetName,
            Headers = headers
        };

        // 2. Read Data Rows
        int lastRow = usedRange.LastRowUsed().RowNumber();
        for (int r = headerRowNumber + 1; r <= lastRow; r++)
        {
            var row = worksheet.Row(r);
            if (row.IsEmpty()) continue;

            var rowMetadata = new RawRowMetadata { RowIndex = r };
            bool hasAnyData = false;

            foreach (var header in headers)
            {
                var cell = row.Cell(header.ColumnLetter);
                if (!string.IsNullOrWhiteSpace(cell.Value.ToString()) || cell.HasFormula) hasAnyData = true;

                // Extract formula
                if (cell.HasFormula)
                {
                    rowMetadata.CellFormulas[header.ColumnLetter] = cell.FormulaA1;
                }

                // Extract value (handling native Excel errors safely without throwing)
                try
                {
                    var val = cell.Value;
                    if (val.IsError)
                    {
                        string errorStr = val.GetError().ToString();
                        rowMetadata.CellValues[header.ColumnLetter] = errorStr;
                        rowMetadata.ExtractionWarnings.Add($"Cell {header.ColumnLetter}{r} contains native error: {errorStr}");
                        
                        if (val.GetError() == XLError.CellReference)
                        {
                            rowMetadata.ExtractionWarnings.Add($"CRITICAL: Cell {header.ColumnLetter}{r} contains broken #REF! reference.");
                        }
                    }
                    else
                    {
                        rowMetadata.CellValues[header.ColumnLetter] = val.ToString();
                    }
                }
                catch (Exception ex)
                {
                    rowMetadata.CellValues[header.ColumnLetter] = "ERROR";
                    rowMetadata.ExtractionWarnings.Add($"Cell {header.ColumnLetter}{r} read error: {ex.Message}");
                }
            }

            if (hasAnyData)
            {
                map.DataRows.Add(rowMetadata);
            }
        }

        return map;
    }
}
