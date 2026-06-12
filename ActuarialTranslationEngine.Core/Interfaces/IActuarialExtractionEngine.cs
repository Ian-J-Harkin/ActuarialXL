using System.IO;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Interfaces;

public interface IActuarialExtractionEngine
{
    List<string> GetWorksheetNames(Stream fileStream);
    RawWorkbookMap ExtractSheetData(Stream fileStream, string sheetName);
}
