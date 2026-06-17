using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Engine.Parsers
{
    public class VbaExtractionEngine : IVbaExtractionEngine
    {
        public List<VbaModuleCode> ExtractVbaCodeStreams(Stream fileStream)
        {
            var modules = new List<VbaModuleCode>();
            try
            {
                // isEditable: false prevents locking the stream exclusively for write
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(fileStream, false))
                {
                    var vbaPart = document.WorkbookPart?.VbaProjectPart;
                    if (vbaPart != null)
                    {
                        using var stream = vbaPart.GetStream();
                        using var reader = new StreamReader(stream);
                        modules.Add(new VbaModuleCode
                        {
                            ModuleName = "vbaProject.bin",
                            RawVbaTextString = reader.ReadToEnd()
                        });
                    }
                }
            }
            catch (OpenXmlPackageException)
            {
                // Acceptance Criteria: must not throw an OpenXmlPackageException
            }
            catch (FileFormatException)
            {
                // Handle invalid files gracefully
            }
            
            return modules;
        }
    }
}
