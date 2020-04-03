using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementRequestHandler
    {
        bool ExecuteAndReturnProposal(EndorsementRequest request);
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
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public bool ExecuteAndReturnProposal(EndorsementRequest request)
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

            if (result.PrivateReadWriteSet.WriteSet.Any())
            {
                // Store any changes that were made to the transient store
                byte[] privateReadWriteSetData = Encoding.UTF8.GetBytes(result.PrivateReadWriteSet.GetReadWriteSet().ToJson()); // ew

                this.transientStore.Persist(request.ContractTransaction.GetHash(), blockHeight, new TransientStorePrivateData(privateReadWriteSetData));

                // TODO: Only do this on the final endorsement, depending on the policy.
                this.BroadcastPrivateDataToOrganisation();
            }

            // TODO: If we have multiple endorsements happening here, check the read write set before signing!
            Transaction signedRWSTransaction = this.readWriteSetTransactionSerializer.Build(result.ReadWriteSet.GetReadWriteSet());

            uint256 proposalId = request.ContractTransaction.GetHash();
            var payload = new EndorsementPayload(signedRWSTransaction, proposalId);

            try
            {
                // Send the result back.
                EndorsementInfo info = this.endorsements.RecordEndorsement(proposalId);               
                info.SetState(EndorsementState.Approved);

                request.Peer.SendMessageAsync(payload).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // This catch is a bit dirty but is copied from FederatedPegBroadcaster code.
            }



            return true;
        }

        private void BroadcastPrivateDataToOrganisation()
        {
            throw new NotImplementedException();
        }
    }
}
