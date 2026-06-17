using System;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Persistence;

public interface IPersistenceManager
{
    Task SaveTranslationAsync(TranslatedModelRecord record, CancellationToken cancellationToken = default);
}
