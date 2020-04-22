using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementRequestHandler
    {
        Task<bool> ExecuteAndReturnProposalAsync(EndorsementRequest request);
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
        private readonly IReadWriteSetTransactionSerializer readWriteSetTransactionSerializer;
        private readonly IEndorsements endorsements;
        private readonly ITransientStore transientStore;
        private readonly ITokenlessBroadcaster tokenlessBroadcaster;
        private readonly ILogger logger;

        public EndorsementRequestHandler(
            IEndorsementRequestValidator validator,
            IEndorsementSigner signer,
            IContractExecutorFactory executorFactory,
            ITokenlessSigner senderRetriever,
            IConsensusManager consensus,
            IStateRepositoryRoot stateRoot,
            IReadWriteSetTransactionSerializer readWriteSetTransactionSerializer,
            IEndorsements endorsements,
            ITransientStore transientStore,
            ITokenlessBroadcaster tokenlessBroadcaster,
            ILoggerFactory loggerFactory)
        {
            this.validator = validator;
            this.signer = signer;
            this.executorFactory = executorFactory;
            this.senderRetriever = senderRetriever;
            this.consensus = consensus;
            this.stateRoot = stateRoot;
            this.readWriteSetTransactionSerializer = readWriteSetTransactionSerializer;
            this.endorsements = endorsements;
            this.transientStore = transientStore;
            this.tokenlessBroadcaster = tokenlessBroadcaster;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task<bool> ExecuteAndReturnProposalAsync(EndorsementRequest request)
        {
            if (!this.validator.ValidateRequest(request))
            {
                this.logger.LogDebug("Request for endorsement was invalid.");
                return false;
            }

            ChainedHeader tip = this.consensus.Tip;
            uint blockHeight = (uint) tip.Height + 1;

            IStateRepositoryRoot stateSnapshot = this.stateRoot.GetSnapshotTo(((ISmartContractBlockHeader)tip.Header).HashStateRoot.ToBytes());
            IContractExecutor executor = this.executorFactory.CreateExecutor(stateSnapshot);

            GetSenderResult getSenderResult = this.senderRetriever.GetSender(request.ContractTransaction); // Because of rule checks in the validator, we assume this is always correct.

            var executionContext = new ContractTransactionContext((ulong) blockHeight,TxIndexToUse,CoinbaseToUse, getSenderResult.Sender, request.ContractTransaction, request.TransientData);

            IContractExecutionResult result = executor.Execute(executionContext);

            if (result.Revert)
            {
                this.logger.LogDebug("Request for endorsement resulted in failed contract execution: {0}", result.ErrorMessage);
                return false;
            }

            // TODO: If we have multiple endorsements happening here, check the read write set before signing!
            SignedProposalResponse signedProposalResponse = this.readWriteSetTransactionSerializer.Build(result.ReadWriteSet.GetReadWriteSet());

            uint256 proposalId = request.ContractTransaction.GetHash();
            var payload = new EndorsementPayload(signedProposalResponse, proposalId);

            EndorsementPolicy contractsPolicy = this.stateRoot.GetPolicy(result.To);

            try
            {
                // Send the result back.
                // TODO do we need to keep track of endorsements outside of the proposer?
                EndorsementInfo info = this.endorsements.RecordEndorsement(proposalId, contractsPolicy);               

                request.Peer.SendMessageAsync(payload).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // This catch is a bit dirty but is copied from FederatedPegBroadcaster code.
            }

            return true;
        }
    }
}
