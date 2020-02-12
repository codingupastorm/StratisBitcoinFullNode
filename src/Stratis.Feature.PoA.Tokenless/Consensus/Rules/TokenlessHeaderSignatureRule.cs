using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Voting;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Estimates which public key should be used for timestamp of a header being
    /// validated and uses this public key to verify header's signature.
    /// </summary>
    public sealed class TokenlessHeaderSignatureRule : HeaderValidationConsensusRule 
    {
        private readonly PoABlockHeaderValidator headerValidator;
        private readonly ISlotsManager slotsManager;
        private readonly VotingManager votingManager;

        public TokenlessHeaderSignatureRule(PoABlockHeaderValidator headerValidator, ISlotsManager slotsManager, VotingManager votingManager)
        {
            this.headerValidator = headerValidator;
            this.slotsManager = slotsManager;
            this.votingManager = votingManager;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Run(RuleContext context)
        {
            var poaHeader = context.ValidationContext.ChainedHeaderToValidate.Header as PoABlockHeader;

            PubKey pubKey = this.slotsManager.GetFederationMemberForTimestamp(poaHeader.Time).PubKey;

            if (!this.headerValidator.VerifySignature(pubKey, poaHeader))
            {
                // If voting is enabled, it is possible that the federation was modified and another federation member signed
                // the header. Since voting changes are only applied after [max reorg] blocks have passed, we can tell exactly
                // how the federation will look like, [max reorg] blocks ahead. The code below tries to construct the federation that is
                // expected to exist at the moment the block that corresponds to header being validated was produced. Then
                // this federation is used to estimate who was expected to sign a block and then the signature is verified.

                IConsensus consensus = this.Parent.Network.Consensus;

                if (((PoAConsensusOptions)consensus.Options).VotingEnabled)
                {
                    ChainedHeader currentHeader = context.ValidationContext.ChainedHeaderToValidate;

                    bool mightBeInsufficient = currentHeader.Height - this.Parent.ChainState.ConsensusTip.Height > consensus.MaxReorgLength;

                    List<IFederationMember> modifiedFederation = this.slotsManager.GetModifiedFederation(this.votingManager, context.ValidationContext.ChainedHeaderToValidate.Height);

                    pubKey = this.slotsManager.GetFederationMemberForTimestamp(poaHeader.Time, modifiedFederation).PubKey;

                    if (this.headerValidator.VerifySignature(pubKey, poaHeader))
                    {
                        this.Logger.LogDebug("Signature verified using updated federation.");
                        return;
                    }

                    if (mightBeInsufficient)
                    {
                        // Mark header as insufficient to avoid banning the peer that presented it.
                        // When we advance consensus we will be able to validate it.
                        context.ValidationContext.InsufficientHeaderInformation = true;
                    }
                }

                this.Logger.LogTrace("(-)[INVALID_SIGNATURE]");
                PoAConsensusErrors.InvalidHeaderSignature.Throw();
            }
        }
    }
}