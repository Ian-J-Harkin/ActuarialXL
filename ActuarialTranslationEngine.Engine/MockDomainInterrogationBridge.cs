using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Engine;

public class MockDomainInterrogationBridge : IDomainInterrogationBridge
{
    public Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TranslationOutput
        {
            FinalAuditableMarkdown = $"# Mock Specification\nTarget: {payload.TargetWorksheet}\nPartitions: {payload.Partitions.Count}",
            GeneratedCSharpMirrorCode = $$"""
                // Received {{payload.Partitions.Count}} partitions for {{payload.TargetWorksheet}}
                using System;
                using System.Collections.Generic;
                using ActuarialTranslationEngine.Core.Interfaces;
                
                public class DynamicReconciliationUnit : IActuarialReconciliationUnit
                {
                    public decimal ExecuteCalculationRow(Dictionary<string, decimal> inputs) => 42m;
                }
                """
        });
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
                    public decimal ExecuteCalculationRow(Dictionary<string, decimal> inputs) => 42m;
                }
                """
        });
    }
}
