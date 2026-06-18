# Story 5.1: EPPlus VBA Binary Extraction

**Status:** review

## Story Foundation
As a system orchestrator,
I want to extract the raw macro binary stream from legacy .xlsm/.xlsb workbooks using EPPlus,
So that the LLM has access to the imperative logic that cannot be parsed by ClosedXML.

### Acceptance Criteria:
**Given** an uploaded .xlsm or .xlsb file containing macros,
**When** the new IVbaExtractionEngine processes the stream,
**Then** it must successfully extract the VBA modules as a list of `VbaModuleCode` objects,
**And** it must use EPPlus to decompress the OLE container and read the source text without locking the file stream.

## Dev Agent Guardrails & Technical Context

### Architecture Compliance
- **Dependency:** Ensure `EPPlus` package is used in `ActuarialTranslationEngine.Engine`.
- **Implementation:** Create `IVbaExtractionEngine` in `ActuarialTranslationEngine.Core.Interfaces` and `VbaExtractionEngine` in `ActuarialTranslationEngine.Engine`.
- **Interface Contract:**
```csharp
public interface IVbaExtractionEngine
{
    // Returns a list of extracted VBA modules
    List<ActuarialTranslationEngine.Core.Models.VbaModuleCode> ExtractVbaCodeStreams(Stream fileStream);
}
```

### File Structure Requirements
- `ActuarialTranslationEngine.Core/Interfaces/IVbaExtractionEngine.cs`
- `ActuarialTranslationEngine.Engine/VbaExtractionEngine.cs`
- `ActuarialTranslationEngine.Tests.Unit/Parsers/VbaExtractionEngineTests.cs`

### Testing Requirements
- Unit tests mocking/loading a `.xlsm` dummy file to verify the binary payload is successfully extracted without locks or exceptions.

---
*Completion note: Ultimate context engine analysis completed - comprehensive developer guide created.*
