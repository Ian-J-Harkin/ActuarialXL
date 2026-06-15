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
    public List<string> GetWorksheetNames(Stream fileStream)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        using var workbook = new XLWorkbook(fileStream);
        return workbook.Worksheets.Select(s => s.Name).ToList();
    }

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

            // 1. Find Header Row (rows 1-500)
            for (int r = 1; r <= Math.Min(500, usedRange.LastRowUsed().RowNumber()); r++)
            {
                var row = worksheet.Row(r);
                var cells = row.CellsUsed();
                if (!cells.Any()) continue; // skip completely empty rows

                // Consider this row as header
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

            if (headerRowNumber == -1)
            {
                throw new ActuarialExtractionException("No header row detected in first 500 rows");
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
                    string formula = cell.FormulaA1;
                    rowMetadata.CellFormulas[header.ColumnLetter] = formula;

                    // Check for Volatile functions
                    if (formula.Contains("RAND()") || formula.Contains("OFFSET(") || formula.Contains("NOW(") || formula.Contains("INDIRECT("))
                    {
                        rowMetadata.DisruptiveNodes.Add(new DisruptiveNode
                        {
                            Coordinate = $"{sheetName}!{header.ColumnLetter}{r}",
                            RawFormula = formula,
                            EvaluatedValue = cell.CachedValue.ToString(), // Hard-lock the frozen value
                            ExceptionFlag = ActuarialNodeExceptionType.VolatileFunction,
                            Telemetry = new Dictionary<string, object> { { "IsVolatile", true } }
                        });
                        // Hard-lock the sample evaluated value in the output
                        rowMetadata.CellValues[header.ColumnLetter] = cell.CachedValue.ToString();
                        hasAnyData = true;
                        continue;
                    }
                }

                // Extract value (handling native Excel errors safely without throwing)
                try
                {
                    var val = cell.Value;
                    if (val.IsError)
                    {
                        string errorStr = val.GetError().ToString();
                        rowMetadata.CellValues[header.ColumnLetter] = $"<ERROR_STATE: {errorStr}>";
                        
                        var node = new DisruptiveNode
                        {
                            Coordinate = $"{sheetName}!{header.ColumnLetter}{r}",
                            RawFormula = cell.HasFormula ? cell.FormulaA1 : string.Empty,
                            EvaluatedValue = $"<ERROR_STATE: {errorStr}>",
                            ExceptionFlag = ActuarialNodeExceptionType.ExcelNativeError
                        };
                        
                        if (val.GetError() == XLError.CellReference)
                        {
                            node.Telemetry.Add("Severity", "CRITICAL");
                            node.Telemetry.Add("Message", "Contains broken #REF! reference");
                        }

                        rowMetadata.DisruptiveNodes.Add(node);
                    }
                    else
                    {
                        rowMetadata.CellValues[header.ColumnLetter] = val.ToString();
                    }
                }
                catch (Exception ex)
                {
                    rowMetadata.CellValues[header.ColumnLetter] = "<ERROR_STATE: READ_EXCEPTION>";
                    rowMetadata.DisruptiveNodes.Add(new DisruptiveNode
                    {
                        Coordinate = $"{sheetName}!{header.ColumnLetter}{r}",
                        ExceptionFlag = ActuarialNodeExceptionType.ExcelNativeError,
                        Telemetry = new Dictionary<string, object> { { "ExceptionMessage", ex.Message } }
                    });
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
