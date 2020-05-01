using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public class EndorsedContractTransactionConsensusRule : PartialValidationConsensusRule
    {
        private readonly EndorsedContractTransactionValidationRule rule;

        public EndorsedContractTransactionConsensusRule(EndorsedContractTransactionValidationRule rule)
        {
            this.rule = rule;
        }

        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                (bool valid, EndorsedContractTransactionValidationRule.EndorsementValidationErrorType error) = this.rule.CheckTransaction(transaction);

                var errorType = EndorsedContractTransactionValidationRule.ErrorMessages[error];

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall)
                {
                    // No further validation needed.
                    continue;
                }

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed)
                {
                    new ConsensusError(errorType, "malformed endorsements").Throw();
                }

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.PolicyInvalid)
                {
                    new ConsensusError(errorType, "endorsement policy not satisfied").Throw();
                }

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid)
                {
                    new ConsensusError(errorType, "endorsement policy signatures invalid").Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
    /// <summary>
    /// Executes a channel creation request if the transaction contains it.
    /// </summary>
    public sealed class ExecuteChannelAddMemberRequest : FullValidationConsensusRule
    {
        private readonly ChannelSettings channelSettings;
        private readonly IChannelRepository channelRepository;
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ILogger<ExecuteChannelAddMemberRequest> logger;

        public ExecuteChannelAddMemberRequest(
            ChannelSettings channelSettings,
            ILoggerFactory loggerFactory,
            IChannelRepository channelRepository,
            IChannelRequestSerializer channelRequestSerializer)
        {
            this.channelSettings = channelSettings;
            this.channelRepository = channelRepository;
            this.channelRequestSerializer = channelRequestSerializer;
            this.logger = loggerFactory.CreateLogger<ExecuteChannelAddMemberRequest>();
        }

        /// <inheritdoc/>
        public override async Task RunAsync(RuleContext context)
        {
            // This rule is only applicable if this node is a system channel node.
            if (!this.channelSettings.IsSystemChannelNode)
                return;

            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                // If the TxOut is null then this transaction does not contain any channel update execution code.
                TxOut txOut = transaction.TryGetChannelAddMemberRequestTxOut();
                if (txOut == null)
                    continue;

                (ChannelAddMemberRequest channelAddMemberRequest, string message) = this.channelRequestSerializer.Deserialize<ChannelAddMemberRequest>(txOut.ScriptPubKey);
                if (channelAddMemberRequest != null)
                {
                    this.logger.LogDebug("Transaction '{0}' contains a request to add member `{1}` to channel '{2}'.", transaction.GetHash(), channelAddMemberRequest.PubKeyHex, channelAddMemberRequest.ChannelName);

                    var memberDefinition = new ChannelMemberDefinition()
                    {
                        ChannelName = channelAddMemberRequest.ChannelName,
                        MemberPublicKey = channelAddMemberRequest.PubKeyHex
                    };

                    this.channelRepository.SaveMemberDefinition(memberDefinition);
                }
            }
        }
    }
}
