namespace ActuarialTranslationEngine.Engine.Roslyn;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Exceptions;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class RoslynReconciliationEngine : IRoslynReconciliationEngine
{
    private readonly LlmBridgeConfiguration _config;

    public RoslynReconciliationEngine(LlmBridgeConfiguration config)
    {
        _config = config;
    }

    public async Task CompileAndVerifyAsync(string csharpCode, Dictionary<string, decimal> rowInputs, decimal expectedSpreadsheetResult, CancellationToken cancellationToken = default)
    {
        // 1. AST Safety Check
        var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode, cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);

        var scanner = new AstSafetyScanner(_config);
        scanner.Visit(root);
        if (scanner.Violations.Any())
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor("AST001", "Security", "Dangerous code: " + string.Join("; ", scanner.Violations), "Security", DiagnosticSeverity.Error, true), Location.None);
            throw new ActuarialDynamicCompilationException("Security scan failed.", new[] { diagnostic });
        }

        // 2. Compilation Setup
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IActuarialReconciliationUnit).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
        };

        var compilation = CSharpCompilation.Create(
            $"ActuarialDynamicAssembly_{Guid.NewGuid()}",
            new[] { syntaxTree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: cancellationToken);

        // 3. Evaluate Compilation Status
        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            throw new ActuarialDynamicCompilationException("Failed to compile LLM generated code.", errors);
        }

        // 4. Memory Stream Reset
        ms.Seek(0, SeekOrigin.Begin);

        // 5. Load Assembly in Collectible Context
        var loadContext = new AssemblyLoadContext("ActuarialReconciliationContext", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(ms);
            var type = assembly.GetType("DynamicReconciliationUnit");

            if (type == null)
            {
                throw new ActuarialLlmBridgeException("Compiled assembly did not contain the class 'DynamicReconciliationUnit'.");
            }

            var instance = Activator.CreateInstance(type);
            if (instance is not IActuarialReconciliationUnit reconciliationUnit)
            {
                throw new ActuarialLlmBridgeException("DynamicReconciliationUnit does not implement IActuarialReconciliationUnit.");
            }

            // 6. Execute with Timeout
            decimal actualResult = 0m;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var executionTask = Task.Run(() => reconciliationUnit.ExecuteCalculationRow(rowInputs), timeoutCts.Token);
            
            try
            {
                actualResult = await executionTask.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new ActuarialLogicLeakException("Execution exceeded the 5-second timeout limit.");
            }

            // 7. Verify Mathematical Variance
            decimal variance = Math.Abs(actualResult - expectedSpreadsheetResult);
            if (variance > 0.00001m)
            {
                throw new ActuarialLogicLeakException($"Mathematical variance exceeded threshold. Expected: {expectedSpreadsheetResult}, Actual: {actualResult}, Variance: {variance}");
            }
        }
        finally
        {
            // Ensure unloading to prevent memory leaks across thousands of rows
            loadContext.Unload();
        }
    }
}
