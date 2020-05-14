using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Validation;

namespace Stratis.SmartContracts.Tools.Sct
{
    public class SctDeterminismValidator
    {
        private static readonly ISmartContractValidator Validator = new SmartContractDeterminismValidator();

        public SmartContractValidationResult Validate(IContractModuleDefinition moduleDefinition)
        {
            return Validator.Validate(moduleDefinition.ModuleDefinition);
        }
    }
}