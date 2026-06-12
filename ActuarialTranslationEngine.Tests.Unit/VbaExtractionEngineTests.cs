using System.IO;
using System.Linq;
using ActuarialTranslationEngine.Engine;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit;

public class VbaExtractionEngineTests
{
    private const string FixturePath = @"C:\Github\ActuarialXLpoc\ActuarialTranslationEngine.Tests.Unit\Fixtures\dummy-macro.xlsm";

    [Fact]
    public void ExtractVbaCodeStreams_ReturnsModuleCode()
    {
        // Arrange
        // Note: The FixtureGenerator MUST have been run prior to this test.
        // It generates dummy-macro.xlsm inside the Fixtures folder.
        Assert.True(File.Exists(FixturePath), "The fixture dummy-macro.xlsm must exist. Run FixtureGenerator first.");
        
        var engine = new VbaExtractionEngine();

        // Act
        using var stream = new FileStream(FixturePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var modules = engine.ExtractVbaCodeStreams(stream);

        // Assert
        Assert.NotNull(modules);
        Assert.NotEmpty(modules);
        
        var targetModule = modules.FirstOrDefault(m => m.ModuleName == "LegacyPricingLogic");
        Assert.NotNull(targetModule);
        Assert.Contains("CalculateLegacyPremium", targetModule.RawVbaTextString);
        Assert.Contains("For i = 1 To 10", targetModule.RawVbaTextString);
    }
}
