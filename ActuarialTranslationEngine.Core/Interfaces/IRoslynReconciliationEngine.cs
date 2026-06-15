namespace ActuarialTranslationEngine.Core.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IRoslynReconciliationEngine
{
    Task CompileAndVerifyAsync(string csharpCode, Dictionary<string, decimal> rowInputs, decimal expectedSpreadsheetResult, CancellationToken cancellationToken = default);
}
