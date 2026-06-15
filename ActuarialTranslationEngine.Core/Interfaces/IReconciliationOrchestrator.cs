namespace ActuarialTranslationEngine.Core.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Models;

public interface IReconciliationOrchestrator
{
    Task ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap workbookMap, CancellationToken cancellationToken = default);
}
