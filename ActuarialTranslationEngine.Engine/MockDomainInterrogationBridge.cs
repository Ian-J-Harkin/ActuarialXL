using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Engine;

public class MockDomainInterrogationBridge : IDomainInterrogationBridge
{
    public async Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string targetColumn, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        // Mock output that calculates G from E to satisfy unit tests
        var mockCSharp = @"using System; 
using System.Collections.Generic; 
using ActuarialTranslationEngine.Core.Interfaces; 

public class DynamicReconciliationUnit : IActuarialReconciliationUnit 
{ 
    public decimal ExecuteCalculationRow(IDictionary<string, decimal> inputs) => inputs.ContainsKey(""E"") ? inputs[""E""] : 0m; 
}";
        return new TranslationOutput
        {
            FinalAuditableMarkdown = $"# Mock Specification\nTarget: {payload.TargetWorksheet}\nPartitions: {payload.Partitions.Count}",
            GeneratedCSharpMirrorCode = mockCSharp
        };
    }

    public Task<TranslationOutput> ProcessVbaPayloadAsync(VbaModuleCode payload, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TranslationOutput
        {
            FinalAuditableMarkdown = $"# Mock VBA Translation\nTarget Module: {payload.ModuleName}",
            GeneratedCSharpMirrorCode = $$"""
                // Translated module: {{payload.ModuleName}}
                using System;
                using System.Collections.Generic;
                using ActuarialTranslationEngine.Core.Interfaces;
                
                public class DynamicReconciliationUnit : IActuarialReconciliationUnit
                {
                    public decimal ExecuteCalculationRow(IDictionary<string, decimal> inputs) => 42m;
                }
                """
        });
    }
}
