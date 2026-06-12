using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Interfaces;

public interface IDomainInterrogationBridge
{
    Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload);
}
