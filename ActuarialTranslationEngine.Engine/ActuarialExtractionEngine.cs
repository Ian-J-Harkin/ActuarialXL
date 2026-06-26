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
        try
        {
            using var workbook = new XLWorkbook(fileStream);
            return workbook.Worksheets.Select(s => s.Name).ToList();
        }
        catch (Exception ex)
        {
            throw new ActuarialExtractionException("The provided stream is not a valid Excel package.", ex);
        }
    }

    public RawWorkbookMap ExtractSheetData(Stream fileStream, string sheetName)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception ex)
        {
            throw new ActuarialExtractionException("The provided stream is not a valid Excel package.", ex);
        }

        using var wb = workbook;
        if (!wb.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
        {
            var available = string.Join(", ", wb.Worksheets.Select(s => s.Name));
            throw new ActuarialExtractionException($"Sheet '{sheetName}' not found. Available sheets: {available}");
        }

        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
        {
            throw new ActuarialExtractionException("Sheet has no used data range");
        }

        // The variables headerRowNumber and headers are declared later in the method.

        // 1. Find Header Row (rows 1-100): Heuristic = highest scoring row
        int searchLimit = Math.Min(100, usedRange.LastRowUsed().RowNumber());
        int bestRowIndex = 0;
        double maxScore = -1;
        
        for (int r = 1; r <= searchLimit; r++)
        {
            var row = worksheet.Row(r);
            var cells = row.CellsUsed().ToList();
            if (!cells.Any()) continue;

            if (cells.Any(c => c.HasFormula))
            {
                // Disqualified: Formulas indicate data
                continue;
            }

            int stringCount = cells.Count(c => c.DataType == XLDataType.Text);
            int numberCount = cells.Count(c => c.DataType == XLDataType.Number || c.DataType == XLDataType.DateTime);

            double score = (stringCount * 10.0) + (numberCount * 1.0) + (cells.Count * 0.1);

            // Strict rule: pure numbers in a single column shouldn't score high enough to beat pure data logic later
            // Actually, if score > 0, it competes. If there's a tie, the first row wins due to > (not >=)
            if (score > maxScore)
            {
                // But wait, if it's purely a single number (score 1.1), we probably shouldn't accept it as a header
                // Let's enforce that a single number isn't a header
                if (stringCount == 0 && cells.Count == 1)
                {
                    continue; // Disqualified: Single number is data
                }

                maxScore = score;
                bestRowIndex = r;
            }
        }

        int headerRowNumber = 0;
        var headers = new List<ColumnDefinition>();
        bool missingHeaderAnomaly = false;

        if (bestRowIndex > 0 && maxScore > 0)
        {
            headerRowNumber = bestRowIndex;
            var headerRow = worksheet.Row(headerRowNumber);
            foreach (var cell in headerRow.CellsUsed())
            {
                if (string.IsNullOrWhiteSpace(cell.GetString())) continue;
                headers.Add(new ColumnDefinition
                {
                    ColumnLetter = cell.WorksheetColumn().ColumnLetter(),
                    ExtractedHeaderName = cell.GetString().Trim()
                });
            }
        }
        else
        {
            // Fallback: No valid header found. Auto-generate headers.
            headerRowNumber = 0;
            missingHeaderAnomaly = true;
            foreach (var col in usedRange.ColumnsUsed().OrderBy(c => c.ColumnNumber()))
            {
                headers.Add(new ColumnDefinition
                {
                    ColumnLetter = col.WorksheetColumn().ColumnLetter(),
                    ExtractedHeaderName = $"Column_{col.WorksheetColumn().ColumnLetter()}"
                });
            }
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

            if (missingHeaderAnomaly)
            {
                rowMetadata.DisruptiveNodes.Add(new DisruptiveNode
                {
                    Coordinate = $"{sheetName}!{headers.First().ColumnLetter}{r}",
                    EvaluatedValue = "Auto-generated generic headers.",
                    ExceptionFlag = ActuarialNodeExceptionType.MissingHeaderRow,
                    Telemetry = new Dictionary<string, object> { { "Severity", "WARNING" }, { "Message", "Sheet lacks a valid text header row." } }
                });
                missingHeaderAnomaly = false; // Only flag it on the first row
                hasAnyData = true;
            }

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
