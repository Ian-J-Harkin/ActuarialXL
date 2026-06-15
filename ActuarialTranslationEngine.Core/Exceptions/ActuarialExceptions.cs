using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ActuarialTranslationEngine.Core.Exceptions;

public class ActuarialExtractionException : Exception
{
    public ActuarialExtractionException(string message) : base(message) { }
    public ActuarialExtractionException(string message, Exception inner) : base(message, inner) { }
}

public class ActuarialLogicLeakException : Exception
{
    public ActuarialLogicLeakException(string message) : base(message) { }
    public ActuarialLogicLeakException(string message, Exception inner) : base(message, inner) { }
}

public class ActuarialDynamicCompilationException : Exception
{
    public IEnumerable<Diagnostic> Diagnostics { get; }

    public ActuarialDynamicCompilationException(string message) : base(message) 
    {
        Diagnostics = Array.Empty<Diagnostic>();
    }
    
    public ActuarialDynamicCompilationException(string message, IEnumerable<Diagnostic> diagnostics) : base(message) 
    {
        Diagnostics = diagnostics;
    }
}

public class ActuarialLlmBridgeException : Exception
{
    public ActuarialLlmBridgeException(string message) : base(message) { }
    public ActuarialLlmBridgeException(string message, Exception inner) : base(message, inner) { }
}
