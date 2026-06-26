using System;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Engine;
using ActuarialTranslationEngine.Engine.LlmBridge;
using ActuarialTranslationEngine.Engine.Orchestration;
using ActuarialTranslationEngine.Engine.Roslyn;
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

            var e2eOption = new Option<bool>("--e2e", () => false,
                "Run the full End-to-End LLM Translation + Roslyn Reconciliation pipeline (requires ACTUARIAL_LLM_API_KEY)");

            var rootCommand = new RootCommand("ActuarialTranslationEngine.CLI")
            {
                fileOption,
                sheetOption,
                outputOption,
                archetypeOption,
                verboseOption,
                dryRunOption,
                e2eOption
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
                    
                    // Enforce Phase III-B Boundary Rule: Load Prompt from Governance Log
                    services.AddSingleton<LlmBridgeConfiguration>(provider =>
                    {
                        var cfg = new LlmBridgeConfiguration();
                        var key = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_API_KEY");
                        if (!string.IsNullOrWhiteSpace(key))
                            cfg.ApiKey = key;
                        else
                            throw new InvalidOperationException("API key not set in environment variable ACTUARIAL_LLM_API_KEY");

                        var endpoint = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_ENDPOINT");
                        if (!string.IsNullOrWhiteSpace(endpoint))
                            cfg.EndpointUrl = endpoint;

                        // Resolve path directly from AppContext to avoid brittle traversal
                        string promptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system-prompt.txt");
                        
                        if (System.IO.File.Exists(promptPath))
                        {
                            try 
                            {
                                cfg.SystemPrompt = System.IO.File.ReadAllText(promptPath).Trim();
                                if (string.IsNullOrWhiteSpace(cfg.SystemPrompt))
                                    throw new InvalidOperationException("Prompt file is empty or whitespace.");
                            }
                            catch (Exception ex)
                            {
                                var logger = provider.GetService<ILogger<Program>>();
                                logger?.LogCritical(ex, "Failed to read system prompt file.");
                                throw;
                            }
                        }
                        else
                        {
                            var logger = provider.GetService<ILogger<Program>>();
                            logger?.LogWarning($"Prompt engineering log not found at {promptPath}. Using default prompt.");
                            cfg.SystemPrompt = "Default testing prompt";
                        }

                        return cfg;
                    });
                    
                    // Register HttpClient for LiveDomainInterrogationBridge with Polly Retry
                    services.AddHttpClient<ActuarialTranslationEngine.Engine.LlmBridge.LiveDomainInterrogationBridge>()
                        .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
                            .HandleTransientHttpError()
                            .Or<System.IO.IOException>()
                            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                                onRetry: (outcome, timespan, retryAttempt, context) =>
                                {
                                    Console.WriteLine($"\n[Polly] Transient network error detected. Retrying attempt {retryAttempt} in {timespan.TotalSeconds} seconds...\n");
                                }));
                    
                    services.AddSingleton<IDomainInterrogationBridge, ActuarialTranslationEngine.Engine.LlmBridge.LiveDomainInterrogationBridge>();

                    // Phase VIII — Register Roslyn engine + Reconciliation Orchestrator for E2E pipeline
                    services.AddSingleton<IRoslynReconciliationEngine, RoslynReconciliationEngine>();
                    services.AddSingleton<IReconciliationOrchestrator, ReconciliationOrchestrator>();

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
                var e2e = context.ParseResult.GetValueForOption(e2eOption);

                int exitCode = await orchestrator.RunAsync(file!, sheet, output!, archetype, verbose, dryRun, e2e);
                context.ExitCode = exitCode;
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}
