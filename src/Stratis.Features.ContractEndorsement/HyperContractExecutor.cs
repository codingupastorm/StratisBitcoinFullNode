using System;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Features.ContractEndorsement.State;
using Stratis.HyperContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.Features.ContractEndorsement
{
    public class HyperContractExecutor
    {
        private readonly ILoader assemblyLoader;

        private readonly IReadableContractStateDb contractStateDb;

        private readonly IContractModuleDefinitionReader moduleDefinitionReader;
        private readonly IContractAssemblyCache assemblyCache;

        public HyperContractExecutor(
            ILoader assemblyLoader,
            IContractModuleDefinitionReader moduleDefinitionReader,
            IContractAssemblyCache contractAssemblyCache,
            IReadableContractStateDb contractStateDb)
        {
            this.assemblyLoader = assemblyLoader;
            this.contractStateDb = contractStateDb;
            this.moduleDefinitionReader = moduleDefinitionReader;
            this.assemblyCache = contractAssemblyCache;
        }

        public ContractExecutionResult Create(
            byte[] contractCode,
            object[] parameters,
            string typeName,
            uint160 contractAddress)
        {
            // Create a stateDb for it to update.
            var cachedStateDb = new CachedStateDb(this.contractStateDb);

            // TODO: Obviously insert something useful here. Use the cached state.
            var hyperContractState = new HyperContractState();

            // The type and code that will ultimately be executed. Assigned based on which method we use to rewrite contract code.
            string typeToInstantiate;
            IHyperContract contract;
            Observer previousObserver = null;

            // Hash the code
            byte[] codeHash = HashHelper.Keccak256(contractCode);
            uint256 codeHashUint256 = new uint256(codeHash);

            // Lets see if we already have an assembly
            CachedAssemblyPackage assemblyPackage = this.assemblyCache.Retrieve(codeHashUint256);

            if (assemblyPackage != null)
            {
                // If the assembly is in the cache, keep a reference to its observer around.
                // We might be in a nested execution for the same assembly,
                // in which case we need to restore the previous observer later.
                previousObserver = assemblyPackage.Assembly.GetObserver();

                typeToInstantiate = typeName ?? assemblyPackage.Assembly.DeployedType.Name;

                Type type = assemblyPackage.Assembly.GetType(typeToInstantiate);

                contract = HyperContractHolder.CreateUninitialized(type, hyperContractState, contractAddress);


                // TODO: Type not found?

                // TODO: Setting observer error?

                // TODO: Error instantiating contract?

            }
            else
            {
                // Create from scratch
                // Validate then rewrite the entirety of the incoming code.
                Result<IContractModuleDefinition> moduleResult = this.moduleDefinitionReader.Read(contractCode);

                if (moduleResult.IsFailure)
                {
                    return ContractExecutionResult.Successful();
                    // TODO:
                    //return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed,
                    //    "Contract bytecode is not valid IL.");
                }

                using (IContractModuleDefinition moduleDefinition = moduleResult.Value)
                {
                    var rewriter = new ObserverInstanceRewriter();

                    if (!this.Rewrite(moduleDefinition, rewriter))
                        return ContractExecutionResult.Fail("Rewrite module failed.");
                        // return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Rewrite module failed");

                    Result<ContractByteCode> getCodeResult = this.GetByteCode(moduleDefinition);

                    if (!getCodeResult.IsSuccess)
                        return ContractExecutionResult.Fail("Serialize module failed.");
                    // return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Serialize module failed");

                    // Everything worked. Assign what will get executed.
                    typeToInstantiate = typeName ?? moduleDefinition.ContractType.Name;
                    ContractByteCode code = getCodeResult.Value;

                    Result<IHyperContract> contractLoadResult = this.Load(
                        code,
                        typeToInstantiate,
                        contractAddress,
                        hyperContractState);

                    if (!contractLoadResult.IsSuccess)
                    {
                        return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, contractLoadResult.Error);
                    }

                    contract = contractLoadResult.Value;

                    assemblyPackage = new CachedAssemblyPackage(new ContractAssembly(contract.Type.Assembly));

                    // Cache this completely validated and rewritten contract to reuse later.
                    this.assemblyCache.Store(codeHashUint256, assemblyPackage);
                }
            }

            this.LogExecutionContext(contract.State.Block, contract.State.Message, contract.Address);

            // Set the code and the Type before the method is invoked
            repository.SetCode(contract.Address, contractCode);
            repository.SetContractType(contract.Address, typeToInstantiate);

            // Set Observer and load and execute.
            assemblyPackage.Assembly.SetObserver(executionContext.Observer);

            // Invoke the constructor of the provided contract code
            IContractInvocationResult invocationResult = contract.InvokeConstructor(parameters);

            // Always reset the observer, even if the previous was null.
            assemblyPackage.Assembly.SetObserver(previousObserver);

            if (!invocationResult.IsSuccess)
            {
                return GetInvocationVmErrorResult(invocationResult);
            }

            return VmExecutionResult.Ok(invocationResult.Return, typeToInstantiate);

            // Rewrite the contract code to include gas and memory tracking.

            // Load the contract assembly.

            // Add code and type to cached db.

            // Set Observer.

            // Invoke constructor.

            // Re-set observer.

            // Get result and return.


            throw new NotImplementedException();
        }

        public ContractExecutionResult Call(object[] parameters)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Loads the contract bytecode and returns an <see cref="IContract"/> representing an uninitialized contract instance.
        /// </summary>
        private Result<IHyperContract> Load(ContractByteCode byteCode,
            string typeName,
            uint160 address,
            IHyperContractState contractState)
        {
            Result<IContractAssembly> assemblyLoadResult = this.assemblyLoader.Load(byteCode);

            if (!assemblyLoadResult.IsSuccess)
            {

                return Result.Fail<IHyperContract>(assemblyLoadResult.Error);
            }

            IContractAssembly contractAssembly = assemblyLoadResult.Value;

            Type type = contractAssembly.GetType(typeName);

            if (type == null)
            {
                const string typeNotFoundError = "Type not found!";

                return Result.Fail<IHyperContract>(typeNotFoundError);
            }

            IHyperContract contract;

            try
            {
                contract = HyperContractHolder.CreateUninitialized(type, contractState, address);
            }
            catch (Exception e)
            {

                return Result.Fail<IHyperContract>("Exception occurred while instantiating contract instance");
            }

            return Result.Ok(contract);
        }

        private bool Rewrite(IContractModuleDefinition moduleDefinition, IILRewriter rewriter)
        {
            try
            {
                moduleDefinition.Rewrite(rewriter);
                return true;
            }
            catch (Exception e)
            {
                throw new NotImplementedException("Rewrite broke, need to exit cleanly.");
            }

            return false;
        }

        private Result<ContractByteCode> GetByteCode(IContractModuleDefinition moduleDefinition)
        {
            try
            {
                ContractByteCode code = moduleDefinition.ToByteCode();

                return Result.Ok(code);
            }
            catch (Exception e)
            {
                return Result.Fail<ContractByteCode>("Exception occurred while serializing module");
            }
        }
    }

    public class ContractExecutionResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }

        private ContractExecutionResult(bool success, string errorMessage)
        {
            this.Success = success;
            this.ErrorMessage = errorMessage;
        }

        public static ContractExecutionResult Fail(string errorMessage)
        {
            return new ContractExecutionResult(false, errorMessage);
        }

        public static ContractExecutionResult Successful()
        {
            throw new NotImplementedException("Return more");
        }
    }
}