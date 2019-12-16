﻿using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Spawns SmartContractExecutor instances
    /// </summary>
    public class ReflectionExecutorFactory : IContractExecutorFactory
    {
        private readonly IContractRefundProcessor refundProcessor;
        private readonly IContractTransferProcessor transferProcessor;
        private readonly ICallDataSerializer serializer;
        private readonly IStateFactory stateFactory;
        private readonly IStateProcessor stateProcessor;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;

        public ReflectionExecutorFactory(ICallDataSerializer serializer,
            IContractRefundProcessor refundProcessor,
            IContractTransferProcessor transferProcessor,
            IStateFactory stateFactory,
            IStateProcessor stateProcessor,
            IContractPrimitiveSerializer contractPrimitiveSerializer)
        {
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.serializer = serializer;
            this.stateFactory = stateFactory;
            this.stateProcessor = stateProcessor;
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
        }

        /// <summary>
        /// Initialize a smart contract executor for the block assembler or consensus validator. 
        /// <para>
        /// After the contract has been executed, it will process any fees and/or refunds.
        /// </para>
        /// </summary>
        public IContractExecutor CreateExecutor(
            IStateRepositoryRoot stateRepository,
            IContractTransactionContext transactionContext)
        {
            return new ContractExecutor(this.serializer, stateRepository, this.refundProcessor, this.transferProcessor, this.stateFactory, this.stateProcessor, this.contractPrimitiveSerializer);
        }
    }
}