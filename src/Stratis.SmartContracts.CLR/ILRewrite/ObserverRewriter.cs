using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    /// <summary>
    /// Injects a new type with reference to an <see cref="Observer"/> into a module which can be used to track runtime metrics.
    /// </summary>
    public class ObserverRewriter : IILRewriter
    {
        private const string InjectedNamespace = "<Stratis>";
        private const string InjectedPropertyName = "Instance";
        public const string InjectedTypeName = "<RuntimeObserverInstance>";
        public const string ConstructorName = ".cctor";

        /// <summary>
        /// The amount of instructions inserted at the start of every method.
        /// </summary>
        public const int ReservedInstructions = 4;

        /// <summary>
        /// The individual rewriters to be applied to each method, which use the injected type.
        /// </summary>
        private static readonly List<IObserverMethodRewriter> methodRewriters = new List<IObserverMethodRewriter>
        {
            new GasInjectorRewriter(),
            new MemoryLimitRewriter()
        };

        public ObserverRewriter() { }

        /// <summary>
        /// Completely rewrites a module with all of the code required to meter memory and gas.
        /// Includes the injection of the specific observer for this contract.
        /// </summary>
        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            foreach (TypeDefinition type in module.GetTypes())
            {
                this.RewriteType(type);
            }

            return module;
        }

        private void RewriteType(TypeDefinition type)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                this.RewriteMethod(method);
            }
        }

        /// <summary>
        /// Makes the <see cref="Observer"/> available to the given method as a variable and then
        /// applies all of the individual rewriters to the method.
        /// </summary>
        private void RewriteMethod(MethodDefinition methodDefinition)
        {
            if (!methodDefinition.HasBody || methodDefinition.Body.Instructions.Count == 0)
                return; // don't inject on method without a Body 

            ModuleDefinition module = methodDefinition.Module;

            // Inject observer instance to method.
            ILProcessor il = methodDefinition.Body.GetILProcessor();
            var observerVariable = new VariableDefinition(module.ImportReference(typeof(Observer)));
            il.Body.Variables.Add(observerVariable);
            Instruction start = methodDefinition.Body.Instructions[0];

            // Call get unique Id
            // Call observer get
            MethodReference getIdReference = module.ImportReference(typeof(UniqueIdExtensions).GetMethod(nameof(UniqueIdExtensions.GetRefId)));
            MethodReference getGuardInstance = module.ImportReference(typeof(ObserverInstances).GetMethod(nameof(ObserverInstances.Get)));

            // Ensure the number of instructions here matches ReservedInstructions!!
            il.InsertBefore(start, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(start, il.Create(OpCodes.Call, getIdReference));
            il.InsertBefore(start, il.Create(OpCodes.Call, getGuardInstance));
            il.InsertBefore(start, il.CreateStlocBest(observerVariable));

            var context = new ObserverRewriterContext(new ObserverReferences(module), observerVariable);

            foreach(IObserverMethodRewriter rewriter in methodRewriters)
            {
                rewriter.Rewrite(methodDefinition, il, context);
            }
        }
    }
}
