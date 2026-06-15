namespace ActuarialTranslationEngine.Tests.Unit.Roslyn;

using System.Linq;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AstSafetyScannerTests
{
    private readonly LlmBridgeConfiguration _config = new();

    [Fact]
    public void Scanner_ShouldReject_SystemIODirective()
    {
        string code = "using System.IO; public class A { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var scanner = new AstSafetyScanner(_config);
        
        scanner.Visit(tree.GetRoot());
        
        Assert.Contains(scanner.Violations, v => v.Contains("System.IO"));
    }

    [Fact]
    public void Scanner_ShouldReject_ProcessStart()
    {
        string code = "public class A { public void X() { System.Diagnostics.Process.Start(\"cmd.exe\"); } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var scanner = new AstSafetyScanner(_config);
        
        scanner.Visit(tree.GetRoot());
        
        Assert.NotEmpty(scanner.Violations);
    }

    [Fact]
    public void Scanner_ShouldReject_WhileTrue()
    {
        string code = "public class A { public void X() { while(true) { } } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var scanner = new AstSafetyScanner(_config);
        
        scanner.Visit(tree.GetRoot());
        
        Assert.Contains(scanner.Violations, v => v.Contains("while(true)"));
    }

    [Fact]
    public void Scanner_ShouldAllow_CleanActuarialUnit()
    {
        string code = @"
using System;
using System.Collections.Generic;

public class DynamicReconciliationUnit : IActuarialReconciliationUnit
{
    public decimal ExecuteCalculationRow(Dictionary<string, decimal> inputs)
    {
        return inputs[""A""] + inputs[""B""];
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var scanner = new AstSafetyScanner(_config);
        
        scanner.Visit(tree.GetRoot());
        
        Assert.Empty(scanner.Violations);
    }
}
