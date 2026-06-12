using System.IO;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Interfaces;

public interface IActuarialExtractionEngine
{
    RawWorkbookMap ExtractSheetData(Stream fileStream, string sheetName);
}
