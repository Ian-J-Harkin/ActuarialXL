using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine.LlmBridge;
using ActuarialTranslationEngine.Engine.Roslyn;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit
{
    public class MachineryTest
    {
        [Fact]
        public async Task Test_SemanticAdapterPattern_Machinery()
        {
            // 1. Mock the raw LLM output with missing opening backticks and containing BOTH classes
            string rawLlmOutput = @"
Here is your actuarial logic!

public class Table134ActuarialModel
{
    public decimal Calculate(decimal baseValue) 
    {
        return baseValue * 2m;
    }
}

public class DynamicReconciliationUnit : ActuarialTranslationEngine.Core.Interfaces.IActuarialReconciliationUnit
{
    public decimal ExecuteCalculationRow(IDictionary<string, decimal> inputs)
    {
        var model = new Table134ActuarialModel();
        return model.Calculate(inputs[""B""]);
    }
}
```";

            // 2. Test the Bridge's Markdown Parser via Reflection
            var config = new LlmBridgeConfiguration { ApiKey = "dummy", EndpointUrl = "dummy", SystemPrompt = "dummy" };
            var bridge = new LiveDomainInterrogationBridge(new HttpClient(), config);
            
            MethodInfo? parseMethod = typeof(LiveDomainInterrogationBridge).GetMethod("ParseLlmOutput", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(parseMethod);

            var result = (TranslationOutput)parseMethod!.Invoke(bridge, new object[] { rawLlmOutput })!;
            
            string csharpCode = result.GeneratedCSharpMirrorCode;
            Console.WriteLine("=== EXTRACTED CSHARP CODE ===");
            Console.WriteLine(csharpCode);

            // 3. Test the Roslyn Compiler (Ensure decimal references work)
            var roslynEngine = new RoslynReconciliationEngine(config);
            var inputs = new Dictionary<string, decimal> { { "B", 50m } };
            
            // Expected is 100m because 50m * 2m = 100m
            try
            {
                await roslynEngine.CompileAndVerifyAsync(csharpCode, inputs, 100m);
                Console.WriteLine("Roslyn compilation and execution succeeded!");
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(@"c:\Github\ActuarialXLpoc\roslyn_error.txt", ex.ToString());
                throw;
            }
            
            Console.WriteLine("Roslyn compilation and execution succeeded!");
        }
    }
}
