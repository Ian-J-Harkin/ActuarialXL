using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Interfaces;

public interface IVectorCompressionEngine
{
    CompressedVectorBlock CompressTopology(RawWorkbookMap sourceMap);
}
