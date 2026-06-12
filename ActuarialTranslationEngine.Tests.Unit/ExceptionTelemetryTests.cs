using System.Collections.Generic;
using System.IO;
using System.Linq;
using ActuarialTranslationEngine.Core.Exceptions;
using ActuarialTranslationEngine.Engine;
using ClosedXML.Excel;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit
{
    public class ExceptionTelemetryTests
    {
        [Fact]
        public void ExtractionEngine_TrapsExcelNativeError_AsDisruptiveNode()
        {
            // Arrange
            using var stream = new MemoryStream();
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("ErrorSheet");
                ws.Cell("A1").Value = "HeaderA";
                ws.Cell("B1").Value = "HeaderB";
                ws.Cell("C1").Value = "HeaderC";
                
                ws.Cell("A2").Value = 10;
                ws.Cell("B2").Value = 0;
                ws.Cell("C2").FormulaA1 = "A2/B2"; // Triggers #DIV/0! error

                workbook.SaveAs(stream);
            }
            stream.Position = 0;

            var engine = new ActuarialExtractionEngine();

            // Act
            var result = engine.ExtractSheetData(stream, "ErrorSheet");

            // Assert
            var row = result.DataRows.First();
            Assert.Contains(row.DisruptiveNodes, n => n.ExceptionFlag == ActuarialNodeExceptionType.ExcelNativeError);
            var divNode = row.DisruptiveNodes.First(n => n.ExceptionFlag == ActuarialNodeExceptionType.ExcelNativeError);
            Assert.Equal("ErrorSheet!C2", divNode.Coordinate);
            Assert.Contains("DivisionByZero", divNode.EvaluatedValue);
        }

        [Fact]
        public void ExtractionEngine_TrapsVolatileFunction_AsDisruptiveNode()
        {
            // Arrange
            using var stream = new MemoryStream();
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("VolatileSheet");
                ws.Cell("A1").Value = "HeaderA";
                ws.Cell("B1").Value = "HeaderB";
                ws.Cell("C1").Value = "HeaderC";
                
                ws.Cell("A2").Value = 100;
                ws.Cell("B2").FormulaA1 = "RAND()"; // Volatile!
                ws.Cell("C2").FormulaA1 = "OFFSET(A2, 0, 1)"; // Volatile!

                workbook.SaveAs(stream);
            }
            stream.Position = 0;

            var engine = new ActuarialExtractionEngine();

            // Act
            var result = engine.ExtractSheetData(stream, "VolatileSheet");

            // Assert
            var row = result.DataRows.First();
            Assert.Equal(2, row.DisruptiveNodes.Count(n => n.ExceptionFlag == ActuarialNodeExceptionType.VolatileFunction));
            
            var randNode = row.DisruptiveNodes.First(n => n.Coordinate == "VolatileSheet!B2");
            Assert.Equal("RAND()", randNode.RawFormula);

            var offsetNode = row.DisruptiveNodes.First(n => n.Coordinate == "VolatileSheet!C2");
            Assert.Equal("OFFSET(A2, 0, 1)", offsetNode.RawFormula);
        }
    }
}
