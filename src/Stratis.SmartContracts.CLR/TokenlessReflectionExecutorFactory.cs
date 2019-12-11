using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    public class TokenlessReflectionExecutorFactory : IContractExecutorFactory
    {
        private readonly ICallDataSerializer serializer;
        private readonly IStateFactory stateFactory;
        private readonly IStateProcessor stateProcessor;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;

        public TokenlessReflectionExecutorFactory(
            ICallDataSerializer serializer,
            IStateFactory stateFactory,
            IStateProcessor stateProcessor,
            IContractPrimitiveSerializer contractPrimitiveSerializer)
        {
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
            return new TokenlessContractExecutor(this.serializer, stateRepository, this.stateFactory, this.stateProcessor, this.contractPrimitiveSerializer);
        }
    }
}
