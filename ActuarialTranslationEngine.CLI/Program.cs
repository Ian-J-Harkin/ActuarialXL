using System;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Engine;
using ActuarialTranslationEngine.Engine.LlmBridge;
using ActuarialTranslationEngine.Core.Models;
namespace ActuarialTranslationEngine.CLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var fileOption = new Option<string>("--file", "Path to the .xlsx file (required)") { IsRequired = true };
            fileOption.AddAlias("-f");

            var sheetOption = new Option<string?>("--sheet", "Target sheet name (optional; processes all sheets if omitted)");
            sheetOption.AddAlias("-s");

            var outputOption = new Option<string>("--output", () => "./output", "Output directory for JSON payload files");
            outputOption.AddAlias("-o");

            var archetypeOption = new Option<string?>("--archetype", "Filter by archetype: A|B|C|D|E (optional)");
            archetypeOption.AddAlias("-a");

            var verboseOption = new Option<bool>("--verbose", () => false, "Enable detailed logging to stdout");

            var dryRunOption = new Option<bool>("--dry-run", () => false, "Parse and classify only; do not write output files");

            var rootCommand = new RootCommand("ActuarialTranslationEngine.CLI")
            {
                fileOption,
                sheetOption,
                outputOption,
                archetypeOption,
                verboseOption,
                dryRunOption
            };

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(configure => 
                    {
                        configure.ClearProviders();
                        configure.AddConsole();
                    });

                    // Wire up Engine dependencies
                    services.AddSingleton<IActuarialExtractionEngine, ActuarialExtractionEngine>();
                    services.AddSingleton<IVectorCompressionEngine, VectorCompressionEngine>();
                    
                    // Enforce Phase III-A Boundary Rule (Mock Bridge only)
                    // Register LLM bridge configuration from env var
                    services.AddSingleton<LlmBridgeConfiguration>(provider =>
                    {
                        var cfg = new LlmBridgeConfiguration();
                        var key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
                        if (!string.IsNullOrWhiteSpace(key))
                            cfg.ApiKey = key;
                        else
                            throw new InvalidOperationException("OpenRouter API key not set in environment variable OPENROUTER_API_KEY");
                        return cfg;
                    });
                    
                    // Register HttpClient for LiveDomainInterrogationBridge
                    services.AddHttpClient<ActuarialTranslationEngine.Engine.LlmBridge.LiveDomainInterrogationBridge>();
                    services.AddSingleton<IDomainInterrogationBridge, ActuarialTranslationEngine.Engine.LlmBridge.LiveDomainInterrogationBridge>();

                    services.AddSingleton<CLIOrchestrator>();
                })
                .Build();

            var orchestrator = host.Services.GetRequiredService<CLIOrchestrator>();

            rootCommand.SetHandler(async (context) =>
            {
                var file = context.ParseResult.GetValueForOption(fileOption);
                var sheet = context.ParseResult.GetValueForOption(sheetOption);
                var output = context.ParseResult.GetValueForOption(outputOption);
                var archetype = context.ParseResult.GetValueForOption(archetypeOption);
                var verbose = context.ParseResult.GetValueForOption(verboseOption);
                var dryRun = context.ParseResult.GetValueForOption(dryRunOption);

                int exitCode = await orchestrator.RunAsync(file!, sheet, output!, archetype, verbose, dryRun);
                context.ExitCode = exitCode;
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}
