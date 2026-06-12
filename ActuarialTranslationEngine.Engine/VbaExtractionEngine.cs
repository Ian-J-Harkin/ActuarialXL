using System.Collections.Generic;
using System.IO;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using OfficeOpenXml;

namespace ActuarialTranslationEngine.Engine;

public class VbaExtractionEngine : IVbaExtractionEngine
{
    public List<VbaModuleCode> ExtractVbaCodeStreams(Stream fileStream)
    {
        var modules = new List<VbaModuleCode>();

        using var package = new ExcelPackage(fileStream);
        if (package.Workbook.VbaProject != null)
        {
            foreach (var module in package.Workbook.VbaProject.Modules)
            {
                modules.Add(new VbaModuleCode
                {
                    ModuleName = module.Name,
                    RawVbaTextString = module.Code
                });
            }
        }

        return modules;
    }
}
