using System;

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
    public ActuarialDynamicCompilationException(string message) : base(message) { }
    public ActuarialDynamicCompilationException(string message, Exception inner) : base(message, inner) { }
}

public class ActuarialLlmBridgeException : Exception
{
    public ActuarialLlmBridgeException(string message) : base(message) { }
    public ActuarialLlmBridgeException(string message, Exception inner) : base(message, inner) { }
}
