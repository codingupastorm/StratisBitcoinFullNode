using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementRequestHandler
    {
        bool ExecuteAndSignProposal(EndorsementRequest request);
    }

    public class EndorsementRequestHandler : IEndorsementRequestHandler
    {
        private const int TxIndexToUse = 0;
        private static readonly uint160 CoinbaseToUse = uint160.Zero;

        private readonly IEndorsementRequestValidator validator;
        private readonly IEndorsementSigner signer;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ITokenlessSigner senderRetriever;
        private readonly IConsensusManager consensus; // Is this the correct way to get the tip? ChainIndexer not behind interface :(
        private readonly IStateRepositoryRoot stateRoot;
        private readonly ILogger logger;

        public EndorsementRequestHandler(
            IEndorsementRequestValidator validator,
            IEndorsementSigner signer,
            IContractExecutorFactory executorFactory,
            ITokenlessSigner senderRetriever,
            IConsensusManager consensus,
            IStateRepositoryRoot stateRoot,
            ILoggerFactory loggerFactory)
        {
            this.validator = validator;
            this.signer = signer;
            this.executorFactory = executorFactory;
            this.senderRetriever = senderRetriever;
            this.consensus = consensus;
            this.stateRoot = stateRoot;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public bool ExecuteAndSignProposal(EndorsementRequest request)
        {
            if (!this.validator.ValidateRequest(request))
            {
                this.logger.LogDebug("Request for endorsement was invalid.");
                return false;
            }

            ChainedHeader tip = this.consensus.Tip;

            IStateRepositoryRoot stateSnapshot = this.stateRoot.GetSnapshotTo(((ISmartContractBlockHeader)tip.Header).HashStateRoot.ToBytes());
            IContractExecutor executor = this.executorFactory.CreateExecutor(stateSnapshot);

            GetSenderResult getSenderResult = this.senderRetriever.GetSender(request.ContractTransaction); // Because of rule checks in the validator, we assume this is always correct.

            var executionContext = new ContractTransactionContext((ulong) tip.Height,TxIndexToUse,CoinbaseToUse, getSenderResult.Sender, request.ContractTransaction);

            IContractExecutionResult result = executor.Execute(executionContext);

            if (result.Revert)
            {
                this.logger.LogDebug("Request for endorsement resulted in failed contract execution.");
                return false;
            }

            // TODO: We definitely need to check some properties about the read-write set?

            this.signer.Sign(request);
            return true;
        }
    }
}
