namespace ActuarialTranslationEngine.Core.Exceptions
{
    public enum ActuarialNodeExceptionType
    {
        None,
        ExcelNativeError,     // #REF!, #VALUE!, #DIV/0!, #NAME?, #NUM!, #N/A
        VolatileFunction,     // RAND(), NOW(), OFFSET(), INDIRECT()
        CircularReference,    // Intentional or accidental iterative loops
        ExternalWorkbookLink, // Reference to local network drives or missing sheets
        VBAMacroDependency,   // Values written/altered via underlying macro events
        MissingHeaderRow      // Sheet lacks a valid text header row, generic headers auto-generated
    }
}
