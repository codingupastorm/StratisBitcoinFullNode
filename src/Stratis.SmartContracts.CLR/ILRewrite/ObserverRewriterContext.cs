using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    public class ObserverRewriterContext
    {
        public ObserverReferences ObserverReferences { get; }

        public VariableDefinition ObserverVariable { get; }

        public ObserverRewriterContext(ObserverReferences observerReferences, VariableDefinition observerVariable)
        {
            this.ObserverReferences = observerReferences;
            this.ObserverVariable = observerVariable;
        }
    }
}
