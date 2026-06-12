using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Engine;

public class MockDomainInterrogationBridge : IDomainInterrogationBridge
{
    public Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload)
    {
        // In a mock, we just echo back some metadata based on the payload to prove we received it intact
        var output = new TranslationOutput
        {
            GeneratedCSharpMirrorCode = $"// MOCK: Received {payload.Partitions.Count} partitions for {payload.TargetWorksheet}",
            FinalAuditableMarkdown = $"# Mock Specification\nTarget: {payload.TargetWorksheet}\nPartitions: {payload.Partitions.Count}"
        };

        return Task.FromResult(output);
    }
}
