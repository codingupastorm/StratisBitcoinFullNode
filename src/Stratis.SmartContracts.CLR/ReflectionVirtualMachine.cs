﻿using System;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Exceptions;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : IVirtualMachine
    {
        private readonly ILogger logger;
        private readonly ISmartContractValidator validator;
        private readonly ILoader assemblyLoader;
        private readonly IContractModuleDefinitionReader moduleDefinitionReader;
        private readonly IRewrittenContractCache rewrittenContractCache;
        public const int VmVersion = 1;
        public const long MemoryUnitLimit = 100_000;

        public ReflectionVirtualMachine(ISmartContractValidator validator,
            ILoggerFactory loggerFactory,
            ILoader assemblyLoader,
            IContractModuleDefinitionReader moduleDefinitionReader,
            IRewrittenContractCache rewrittenContractCache)
        {
            this.validator = validator;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.assemblyLoader = assemblyLoader;
            this.moduleDefinitionReader = moduleDefinitionReader;
            this.rewrittenContractCache = rewrittenContractCache;
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public VmExecutionResult Create(IStateRepository repository,
            ISmartContractState contractState,
            IGasMeter gasMeter,
            byte[] contractCode,
            object[] parameters,
            string typeName = null)
        {
            // The type and code that will ultimately be executed. Assigned based on which method we use to rewrite contract code.
            string typeToInstantiate;
            ContractByteCode code;

            // Hash the code
            byte[] codeHash = HashHelper.Keccak256(contractCode);
            uint256 codeHashUint256 = new uint256(codeHash);

            // Lets see if we already have a rewritten version of the code. If so, we know it's valid!
            byte[] cachedCode = this.rewrittenContractCache.Retrieve(codeHashUint256);

            if (cachedCode != null)
            {
                // Replace the written-in observer with our custom one in the cached code.
                code = (ContractByteCode)cachedCode;

                Result<IContractModuleDefinition> moduleResult = this.moduleDefinitionReader.Read(cachedCode);

                // Assume this will always work with stored code, else something is wrong and we shouldn't have stored the code in the first place.
                if (moduleResult.IsFailure)
                    throw new Exception("Error loading cached code into module.");

                using (IContractModuleDefinition moduleDefinition = moduleResult.Value)
                {
                    // Everything worked. Assign what will get executed.
                    typeToInstantiate = typeName ?? moduleDefinition.ContractType.Name;
                }
            }
            else
            {
                // Validate then rewrite the entirety of the incoming code.

                Result<IContractModuleDefinition> moduleResult = this.moduleDefinitionReader.Read(contractCode);

                if (moduleResult.IsFailure)
                {
                    this.logger.LogDebug(moduleResult.Error);
                    this.logger.LogTrace("(-)[CONTRACT_BYTECODE_INVALID]");
                    return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, "Contract bytecode is not valid IL.");
                }

                using (IContractModuleDefinition moduleDefinition = moduleResult.Value)
                {
                    SmartContractValidationResult validation = moduleDefinition.Validate(this.validator);

                    // If validation failed, refund the sender any remaining gas.
                    if (!validation.IsValid)
                    {
                        this.logger.LogTrace("(-)[CONTRACT_VALIDATION_FAILED]");
                        return VmExecutionResult.Fail(VmExecutionErrorKind.ValidationFailed, new SmartContractValidationException(validation.Errors).ToString());
                    }

                    var rewriter = new ObserverInstanceRewriter();

                    if (!this.Rewrite(moduleDefinition, rewriter))
                        return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Rewrite module failed");

                    Result<ContractByteCode> getCodeResult = this.GetByteCode(moduleDefinition);

                    if (!getCodeResult.IsSuccess)
                        return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Serialize module failed");

                    // Everything worked. Assign what will get executed.
                    typeToInstantiate = typeName ?? moduleDefinition.ContractType.Name;
                    code = getCodeResult.Value;

                    // Cache this completely validated and rewritten contract to reuse later.
                    this.rewrittenContractCache.Store(codeHashUint256, code.Value);
                }
            }

            var observer = new Observer(gasMeter, new MemoryMeter(MemoryUnitLimit));

            Result<IContract> contractLoadResult = this.Load(
                code,
                typeToInstantiate,
                contractState.Message.ContractAddress.ToUint160(),
                contractState,
                observer);

            if (!contractLoadResult.IsSuccess)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, contractLoadResult.Error);
            }

            IContract contract = contractLoadResult.Value;

            this.LogExecutionContext(contract.State.Block, contract.State.Message, contract.Address);

            // Set the code and the Type before the method is invoked
            repository.SetCode(contract.Address, contractCode);
            repository.SetContractType(contract.Address, typeToInstantiate);

            // Invoke the constructor of the provided contract code
            IContractInvocationResult invocationResult = contract.InvokeConstructor(parameters);

            if (!invocationResult.IsSuccess)
            {
                this.logger.LogDebug("CREATE_CONTRACT_INSTANTIATION_FAILED");
                return GetInvocationVmErrorResult(invocationResult);
            }

            this.logger.LogDebug("CREATE_CONTRACT_INSTANTIATION_SUCCEEDED");

            return VmExecutionResult.Ok(invocationResult.Return, typeToInstantiate);
        }

        /// <summary>
        /// Invokes a method on an existing smart contract
        /// </summary>
        public VmExecutionResult ExecuteMethod(ISmartContractState contractState, IGasMeter gasMeter, MethodCall methodCall, byte[] contractCode, string typeName)
        {
            // The code that will ultimately be executed. Assigned based on which method we use to rewrite contract code.
            ContractByteCode code;

            // Hash the code
            byte[] codeHash = HashHelper.Keccak256(contractCode);
            uint256 codeHashUint256 = new uint256(codeHash);

            // Lets see if we already have a rewritten version of the code.
            byte[] cachedCode = this.rewrittenContractCache.Retrieve(codeHashUint256);

            if (cachedCode != null)
            {
                code = (ContractByteCode)cachedCode;
            }
            else
            {
                // Rewrite from scratch.

                using (IContractModuleDefinition moduleDefinition = this.moduleDefinitionReader.Read(contractCode).Value)
                {
                    var rewriter = new ObserverInstanceRewriter();

                    if (!this.Rewrite(moduleDefinition, rewriter))
                        return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Rewrite module failed");

                    Result<ContractByteCode> getCodeResult = this.GetByteCode(moduleDefinition);

                    if (!getCodeResult.IsSuccess)
                        return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Serialize module failed");

                    // Everything worked. Assign the code that will be executed.
                    code = getCodeResult.Value;

                    // Cache this rewritten contract.
                    this.rewrittenContractCache.Store(codeHashUint256, code.Value);
                }
            }

            var observer = new Observer(gasMeter, new MemoryMeter(MemoryUnitLimit));

            Result<IContract> contractLoadResult = this.Load(
                code,
                typeName,
                contractState.Message.ContractAddress.ToUint160(),
                contractState,
                observer);

            if (!contractLoadResult.IsSuccess)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, contractLoadResult.Error);
            }

            IContract contract = contractLoadResult.Value;

            this.LogExecutionContext(contract.State.Block, contract.State.Message, contract.Address);

            IContractInvocationResult invocationResult = contract.Invoke(methodCall);

            if (!invocationResult.IsSuccess)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_INSTANTIATION_FAILED]");

                return GetInvocationVmErrorResult(invocationResult);
            }

            this.logger.LogDebug("CALL_CONTRACT_INSTANTIATION_SUCCEEDED");

            return VmExecutionResult.Ok(invocationResult.Return, typeName);
        }

        private static VmExecutionResult GetInvocationVmErrorResult(IContractInvocationResult invocationResult)
        {
            if (invocationResult.InvocationErrorType == ContractInvocationErrorType.OutOfGas)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.OutOfGas, invocationResult.ErrorMessage);
            }

            if (invocationResult.InvocationErrorType == ContractInvocationErrorType.OverMemoryLimit)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.OutOfResources, invocationResult.ErrorMessage);
            }

            return VmExecutionResult.Fail(VmExecutionErrorKind.InvocationFailed, invocationResult.ErrorMessage);
        }

        /// <summary>
        /// Loads the contract bytecode and returns an <see cref="IContract"/> representing an uninitialized contract instance.
        /// </summary>
        private Result<IContract> Load(ContractByteCode byteCode,
            string typeName,
            uint160 address,
            ISmartContractState contractState,
            Observer observer)
        {
            Result<IContractAssembly> assemblyLoadResult = this.assemblyLoader.Load(byteCode);

            if (!assemblyLoadResult.IsSuccess)
            {
                this.logger.LogDebug(assemblyLoadResult.Error);

                return Result.Fail<IContract>(assemblyLoadResult.Error);
            }

            IContractAssembly contractAssembly = assemblyLoadResult.Value;

            Type type = contractAssembly.GetType(typeName);

            if (type == null)
            {
                const string typeNotFoundError = "Type not found!";

                this.logger.LogDebug(typeNotFoundError);

                return Result.Fail<IContract>(typeNotFoundError);
            }

            if (!contractAssembly.SetObserver(observer))
            {
                const string setExecutionContextError = "Error setting execution context!";

                return Result.Fail<IContract>(setExecutionContextError);
            }

            IContract contract;

            try
            {
                contract = Contract.CreateUninitialized(type, contractState, address);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());

                return Result.Fail<IContract>("Exception occurred while instantiating contract instance");
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
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[CONTRACT_REWRITE_FAILED]");
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
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[CONTRACT_TOBYTECODE_FAILED]");

                return Result.Fail<ContractByteCode>("Exception occurred while serializing module");
            }
        }

        internal void LogExecutionContext(IBlock block, IMessage message, uint160 contractAddress)
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(block.Coinbase), block.Coinbase, nameof(block.Number), block.Number));
            builder.Append(string.Format("{0}:{1},", nameof(contractAddress), contractAddress.ToAddress()));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));
            this.logger.LogDebug("{0}", builder.ToString());
        }
    }
}