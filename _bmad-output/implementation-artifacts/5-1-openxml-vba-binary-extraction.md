# Story 5.1: OpenXml VBA Binary Extraction

**Status:** review

## Story Foundation
As a system orchestrator,
I want to extract the raw macro binary stream from legacy .xlsm/.xlsb workbooks using DocumentFormat.OpenXml,
So that the LLM has access to the imperative logic that cannot be parsed by ClosedXML.

### Acceptance Criteria:
**Given** an uploaded .xlsm or .xlsb file containing macros,
**When** the new IVbaExtractionEngine processes the stream,
**Then** it must successfully extract the `.vbaProject.bin` stream as raw text,
**And** it must not throw an OpenXmlPackageException or lock the file stream.

## Dev Agent Guardrails & Technical Context

### Architecture Compliance
- **Dependency:** Add `DocumentFormat.OpenXml` package to `ActuarialTranslationEngine.Engine`.
- **Implementation:** Create `IVbaExtractionEngine` in `ActuarialTranslationEngine.Core.Interfaces` and `VbaExtractionEngine` in `ActuarialTranslationEngine.Engine.Parsers`.
- **Interface Contract:**
```csharp
public interface IVbaExtractionEngine
{
    // Returns a dictionary where Key = Module Name, Value = Raw VBA Source Text
    Dictionary<string, string> ExtractMacroModules(Stream fileStream);
}
```

### File Structure Requirements
- `ActuarialTranslationEngine.Core/Interfaces/IVbaExtractionEngine.cs`
- `ActuarialTranslationEngine.Engine/Parsers/VbaExtractionEngine.cs`
- `ActuarialTranslationEngine.Tests.Unit/Parsers/VbaExtractionEngineTests.cs`

### Testing Requirements
- Unit tests mocking/loading a `.xlsm` dummy file to verify the binary payload is successfully extracted without locks or exceptions.

---
*Completion note: Ultimate context engine analysis completed - comprehensive developer guide created.*
