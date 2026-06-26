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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class ReconciliationOrchestratorTests
{
    private async Task<List<TranslationOutput>> ExhaustAsync(IAsyncEnumerable<TranslationOutput> enumerable)
    {
        var list = new List<TranslationOutput>();
        await foreach (var item in enumerable) list.Add(item);
        return list;
    }
    [Fact]
    public async Task ProcessBlockAsync_ThrowsArgumentNullException_IfBlockIsNull()
    {
        var bridge = new MockBridge();
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await ExhaustAsync(sut.ProcessBlockAsync(null!, new RawWorkbookMap { SheetName = "Test" })));
    }

    [Fact]
    public async Task ProcessBlockAsync_ThrowsArgumentNullException_IfMapIsNull()
    {
        var bridge = new MockBridge();
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await ExhaustAsync(sut.ProcessBlockAsync(CreateTestBlock(), null!)));
    }

    [Fact]
    public async Task ProcessBlockAsync_ReturnsEmptyList_IfPartitionsEmpty()
    {
        var bridge = new MockBridge();
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        var block = new CompressedVectorBlock { TargetWorksheet = "Test", Partitions = new List<VectorRangePartition>() };
        var result = await ExhaustAsync(sut.ProcessBlockAsync(block, new RawWorkbookMap { SheetName = "Test" }));

        Assert.Empty(result);
    }

    [Fact]
    public async Task ProcessBlockAsync_PassesOnFirstTry_ValidatesAllSequentialRows()
    {
        // Arrange
        var bridge = new MockBridge { ReturnCode = "public class A { }" };
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap(); // 5 clean rows: rows 5–9

        // Act
        await ExhaustAsync(sut.ProcessBlockAsync(block, map));

        // Assert — Phase VIII: LLM called once per partition, then ALL sequential rows verified
        Assert.Equal(1, bridge.CallCount);
        Assert.Equal(5, engine.CallCount); // All 5 rows (5,6,7,8,9) processed sequentially
    }

    [Fact]
    public async Task ProcessBlockAsync_RetriesOnCompilationFailure_ThenPasses()
    {
        var bridge = new MockBridge { ReturnCode = "public class A { }" };
        var engine = new MockRoslynEngine { FailUntilAttempt = 3 }; // Fails twice, passes on third
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();

        await ExhaustAsync(sut.ProcessBlockAsync(block, map));

        Assert.Equal(3, bridge.CallCount); // Prompted 3 times
    }

    [Fact]
    public async Task ProcessBlockAsync_ReturnsUncertifiedOutput_WhenCompilationRetriesExhausted()
    {
        var bridge = new MockBridge { ReturnCode = "public class A { }" };
        var engine = new MockRoslynEngine { FailUntilAttempt = 4 }; // Fails all 3 times
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();

        var result = await ExhaustAsync(sut.ProcessBlockAsync(block, map));
        Assert.Single(result);
        Assert.False(result[0].IsCertified);
        Assert.Equal(3, bridge.CallCount);
    }

    [Fact]
    public async Task ProcessBlockAsync_RetriesOnLlmTimeout_ThenPasses()
    {
        // Arrange — bridge times out on attempts 1 and 2, succeeds on 3
        var bridge = new TimeoutMockBridge 
        { 
            ReturnCode = "public class A { }",
            TimeoutUntilAttempt = 3 
        };
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();

        // Act
        await ExhaustAsync(sut.ProcessBlockAsync(block, map));

        // Assert — bridge was called 3 times (2 timeouts + 1 success)
        Assert.Equal(3, bridge.CallCount);
    }

    [Fact]
    public async Task ProcessBlockAsync_ReturnsUncertifiedOutput_OnLlmTimeout_WhenAllRetriesExhausted()
    {
        // Arrange — bridge always times out
        var bridge = new TimeoutMockBridge 
        { 
            ReturnCode = "public class A { }",
            TimeoutUntilAttempt = 99 // Always timeout
        };
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();

        // Act & Assert — should exhaust retries and return uncertified
        var result = await ExhaustAsync(sut.ProcessBlockAsync(block, map));
        Assert.Single(result);
        Assert.False(result[0].IsCertified);
        Assert.Equal(3, bridge.CallCount);
    }

    [Fact]
    public async Task ProcessBlockAsync_DoesNotRetryOnUserCancellation()
    {
        // Arrange — simulate user clicking cancel
        var bridge = new MockBridge { ReturnCode = "public class A { }" };
        var engine = new MockRoslynEngine();
        var sut = new ReconciliationOrchestrator(bridge, engine, new NullLogger<ReconciliationOrchestrator>());

        var block = CreateTestBlock();
        var map = CreateTestWorkbookMap();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Act & Assert — should throw immediately, not retry
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await ExhaustAsync(sut.ProcessBlockAsync(block, map, cancellationToken: cts.Token)));
        Assert.Equal(0, bridge.CallCount); // Should never even call the bridge
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

    public Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string targetColumn, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(new TranslationOutput 
        { 
            FinalAuditableMarkdown = "Test", 
            GeneratedCSharpMirrorCode = ReturnCode 
        });
    }

    public Task<TranslationOutput> ProcessVbaPayloadAsync(VbaModuleCode payload, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(new TranslationOutput 
        { 
            FinalAuditableMarkdown = "Test VBA", 
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

/// <summary>
/// Mock bridge that throws OperationCanceledException (simulating an HttpClient timeout)
/// on the first N attempts, then succeeds.
/// </summary>
public class TimeoutMockBridge : IDomainInterrogationBridge
{
    public int CallCount { get; private set; }
    public string ReturnCode { get; set; } = string.Empty;
    public int TimeoutUntilAttempt { get; set; } = 0;

    public Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string targetColumn, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (CallCount < TimeoutUntilAttempt)
        {
            throw new OperationCanceledException("The operation was canceled due to timeout.");
        }
        return Task.FromResult(new TranslationOutput 
        { 
            FinalAuditableMarkdown = "Test", 
            GeneratedCSharpMirrorCode = ReturnCode 
        });
    }

    public Task<TranslationOutput> ProcessVbaPayloadAsync(VbaModuleCode payload, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(new TranslationOutput 
        { 
            FinalAuditableMarkdown = "Test VBA", 
            GeneratedCSharpMirrorCode = ReturnCode 
        });
    }
}
