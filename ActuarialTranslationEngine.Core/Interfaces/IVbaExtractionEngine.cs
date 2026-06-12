using System.Collections.Generic;
using System.IO;

namespace ActuarialTranslationEngine.Core.Interfaces;

public interface IVbaExtractionEngine
{
    List<Models.VbaModuleCode> ExtractVbaCodeStreams(Stream fileStream);
}
