using System.Collections.Generic;
using ActuarialTranslationEngine.Core.Exceptions;

namespace ActuarialTranslationEngine.Core.Models
{
    public class DisruptiveNode
    {
        public string Coordinate { get; set; } = string.Empty;
        public string RawFormula { get; set; } = string.Empty;
        public string EvaluatedValue { get; set; } = string.Empty;
        public ActuarialNodeExceptionType ExceptionFlag { get; set; } = ActuarialNodeExceptionType.None;
        
        // Optional telemetry for downstream handling
        public Dictionary<string, object> Telemetry { get; set; } = new();
    }
}
