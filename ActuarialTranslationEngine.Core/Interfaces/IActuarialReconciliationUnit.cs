using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Interfaces;

public interface IActuarialReconciliationUnit
{
    decimal ExecuteCalculationRow(Dictionary<string, decimal> inputs);
}
