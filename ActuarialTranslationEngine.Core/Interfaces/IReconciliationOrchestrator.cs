namespace ActuarialTranslationEngine.Core.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Models;

public interface IReconciliationOrchestrator
{
    System.Collections.Generic.IAsyncEnumerable<TranslationOutput> ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap workbookMap, IProgress<TranslationProgressEvent>? progress = null, CancellationToken cancellationToken = default);
    Task<List<TranslationOutput>> ProcessVbaModulesAsync(List<VbaModuleCode> modules, CancellationToken cancellationToken = default);
}
