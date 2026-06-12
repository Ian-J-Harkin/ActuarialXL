using System.IO;
using OfficeOpenXml;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit.Fixtures;

public class FixtureGenerator
{
    private const string FixturePath = @"C:\Github\ActuarialXLpoc\ActuarialTranslationEngine.Tests.Unit\Fixtures\dummy-macro.xlsm";

    [Fact]
    public void GenerateDummyMacroWorkbook()
    {
        // Only run this if the file doesn't exist to prevent constantly recreating it
        if (File.Exists(FixturePath)) return;

        // EPPlus 4.x doesn't strictly require LicenseContext, but good practice
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("LegacySheet");
        
        // Add a mock VBA Project
        package.Workbook.CreateVBAProject();
        
        // Add a dummy macro module
        var module = package.Workbook.VbaProject.Modules.AddModule("LegacyPricingLogic");
        module.Code = 
@"Option Explicit

Public Function CalculateLegacyPremium(baseRate As Double) As Double
    ' Obfuscated legacy logic that the LLM needs to clean up
    Dim i As Integer
    Dim multiplier As Double
    multiplier = 1.05
    
    For i = 1 To 10
        baseRate = baseRate * multiplier
    Next i
    
    CalculateLegacyPremium = baseRate
End Function";

        var fileInfo = new FileInfo(FixturePath);
        package.SaveAs(fileInfo);

        Assert.True(File.Exists(FixturePath));
    }
}
