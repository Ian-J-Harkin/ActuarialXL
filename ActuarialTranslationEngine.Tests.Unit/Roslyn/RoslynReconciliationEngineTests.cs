namespace ActuarialTranslationEngine.Tests.Unit.Roslyn;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Exceptions;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine.Roslyn;
using Xunit;

public class RoslynReconciliationEngineTests
{
    private readonly RoslynReconciliationEngine _sut = new(new LlmBridgeConfiguration());

    [Fact]
    public async Task CompileAndVerify_ShouldPass_WhenMathIsCorrect()
    {
        string code = @"
using System;
using System.Collections.Generic;
using ActuarialTranslationEngine.Core.Interfaces;

public class DynamicReconciliationUnit : IActuarialReconciliationUnit
{
    public decimal ExecuteCalculationRow(IDictionary<string, decimal> inputs)
    {
        return inputs[""A""] + inputs[""B""];
    }
}";
        var inputs = new Dictionary<string, decimal> { { "A", 10.5m }, { "B", 20.0m } };
        
        // Expected is 30.5
        await _sut.CompileAndVerifyAsync(code, inputs, 30.5m);
        // If it doesn't throw, it passes.
    }

    [Fact]
    public async Task CompileAndVerify_ShouldThrowLogicLeak_WhenMathIsIncorrect()
    {
        string code = @"
using System;
using System.Collections.Generic;
using ActuarialTranslationEngine.Core.Interfaces;

public class DynamicReconciliationUnit : IActuarialReconciliationUnit
{
    public decimal ExecuteCalculationRow(IDictionary<string, decimal> inputs)
    {
        return inputs[""A""] * inputs[""B""]; // Wrong operator
    }
}";
        var inputs = new Dictionary<string, decimal> { { "A", 10m }, { "B", 20m } };
        
        // Expected is 30, but code returns 200
        await Assert.ThrowsAsync<ActuarialLogicLeakException>(() => 
            _sut.CompileAndVerifyAsync(code, inputs, 30m));
    }

    [Fact]
    public async Task CompileAndVerify_ShouldThrowCompilationException_WhenSafetyScannerFails()
    {
        string code = @"
using System.IO;
using System.Collections.Generic;
using ActuarialTranslationEngine.Core.Interfaces;

public class DynamicReconciliationUnit : IActuarialReconciliationUnit
{
    public decimal ExecuteCalculationRow(IDictionary<string, decimal> inputs) { return 0m; }
}";
        var inputs = new Dictionary<string, decimal>();
        
        var ex = await Assert.ThrowsAsync<ActuarialDynamicCompilationException>(() => 
            _sut.CompileAndVerifyAsync(code, inputs, 0m));
        Assert.Contains("AST001", ex.Diagnostics.First().Id);
    }
}
