namespace ActuarialTranslationEngine.Tests.Unit.Orchestration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Exceptions;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine.Orchestration;
using Microsoft.CodeAnalysis;
using Xunit;

public class ReconciliationOrchestratorTests
{
    [Fact]
    public async Task ProcessBlockAsync_PassesOnFirstTry_With3RowValidation()
    {
        // Arrange
        var bridge = new MockBridge { ReturnCode = "public class A { }" };
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine);

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();

        // Act
        await sut.ProcessBlockAsync(block, map);

        // Assert
        Assert.Equal(1, bridge.CallCount);
        Assert.Equal(3, engine.CallCount); // First, Mid, Last rows validated
    }

    [Fact]
    public async Task ProcessBlockAsync_RetriesOnCompilationFailure_ThenPasses()
    {
        var bridge = new MockBridge { ReturnCode = "public class A { }" };
        var engine = new MockRoslynEngine { FailUntilAttempt = 3 }; // Fails twice, passes on third
        var sut = new ReconciliationOrchestrator(bridge, engine);

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();

        await sut.ProcessBlockAsync(block, map);

        Assert.Equal(3, bridge.CallCount); // Prompted 3 times
    }

    [Fact]
    public async Task ProcessBlockAsync_ThrowsCompilationException_WhenRetriesExhausted()
    {
        var bridge = new MockBridge { ReturnCode = "public class A { }" };
        var engine = new MockRoslynEngine { FailUntilAttempt = 4 }; // Fails all 3 times
        var sut = new ReconciliationOrchestrator(bridge, engine);

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();

        await Assert.ThrowsAsync<ActuarialDynamicCompilationException>(() => sut.ProcessBlockAsync(block, map));
        Assert.Equal(3, bridge.CallCount);
    }

    private CompressedVectorBlock CreateTestBlock()
    {
        return new CompressedVectorBlock
        {
            TargetWorksheet = "Test",
            Partitions = new List<VectorRangePartition>
            {
                new VectorRangePartition
                {
                    StartRow = 5,
                    EndRow = 9,
                    FormulaSignature = "A+B",
                    StructuralColumns = new List<ColumnDefinition>
                    {
                        new ColumnDefinition { ColumnLetter = "A", ExtractedHeaderName = "Input" },
                        new ColumnDefinition { ColumnLetter = "B", ExtractedHeaderName = "Total Output" } // Target heuristic matches 'Total'
                    }
                }
            }
        };
    }

    private RawWorkbookMap CreateTestWorkbookMap()
    {
        var map = new RawWorkbookMap { SheetName = "Test" };
        // Row 6 (First), 7 (Mid), 9 (Last)
        for (int i = 5; i <= 9; i++)
        {
            map.DataRows.Add(new RawRowMetadata
            {
                RowIndex = i,
                CellValues = new Dictionary<string, string> { { "A", "10" }, { "B", "20" } }
            });
        }
        return map;
    }
}

public class MockBridge : IDomainInterrogationBridge
{
    public int CallCount { get; private set; }
    public string ReturnCode { get; set; } = string.Empty;

    public Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(new TranslationOutput 
        { 
            FinalAuditableMarkdown = "Test", 
            GeneratedCSharpMirrorCode = ReturnCode 
        });
    }
}

public class MockRoslynEngine : IRoslynReconciliationEngine
{
    public int CallCount { get; private set; }
    public int FailUntilAttempt { get; set; } = 0;
    private int _attempt = 0;

    public Task CompileAndVerifyAsync(string csharpCode, Dictionary<string, decimal> rowInputs, decimal expectedSpreadsheetResult, CancellationToken cancellationToken = default)
    {
        CallCount++;
        _attempt++;
        
        if (_attempt < FailUntilAttempt)
        {
            throw new ActuarialDynamicCompilationException("Failed", Array.Empty<Diagnostic>());
        }

        return Task.CompletedTask;
    }
}
