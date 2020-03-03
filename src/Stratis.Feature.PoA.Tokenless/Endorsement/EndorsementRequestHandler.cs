using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementRequestHandler
    {
        private const int TxIndexToUse = 0;
        private static readonly uint160 CoinbaseToUse = uint160.Zero;

        private readonly IEndorsementRequestValidator validator;
        private readonly IEndorsementSigner signer;
        private readonly IContractExecutor executor;
        private readonly ITokenlessSigner senderRetriever;
        private readonly IConsensusManager consensus; // Is this the correct way to get the tip? ChainIndexer not behind interface :(
        private readonly ILogger logger;

        public EndorsementRequestHandler(
            IEndorsementRequestValidator validator,
            IEndorsementSigner signer,
            IContractExecutor executor,
            ITokenlessSigner senderRetriever,
            IConsensusManager consensus,
            ILoggerFactory loggerFactory)
        {
            this.validator = validator;
            this.signer = signer;
            this.executor = executor;
            this.senderRetriever = senderRetriever;
            this.consensus = consensus;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public bool ExecuteAndSignProposal(EndorsementRequest request)
        {
            if (!this.validator.ValidateRequest(request))
            {
                this.logger.LogDebug("Request for endorsement was invalid.");
                return false;
            }

            // TODO: Check that multiple things don't execute in the executor at the same time (e.g. blocks validated at same time as endorsement happening)

            // Because of rule checks in the validator, we assume this is always correct.
            GetSenderResult getSenderResult = this.senderRetriever.GetSender(request.ContractTransaction);

            var executionContext = new ContractTransactionContext((ulong) this.consensus.Tip.Height,TxIndexToUse,CoinbaseToUse, getSenderResult.Sender, request.ContractTransaction);
            IContractExecutionResult result = this.executor.Execute(executionContext);

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
