using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Features.PoA.Voting;

namespace Stratis.Features.PoA.BasePoAFeatureConsensusRules
{
    /// <summary>
    /// Estimates which public key should be used for timestamp of a header being
    /// validated and uses this public key to verify header's signature.
    /// </summary>
    public class PoAHeaderSignatureRule : HeaderValidationConsensusRule
    {
        private PoABlockHeaderValidator validator;

        private ISlotsManager slotsManager;

        private uint maxReorg;

        private IChainState chainState;

        private IModifiedFederation modifiedFederation;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            var engine = this.Parent as PoAConsensusRuleEngine;

            this.slotsManager = engine.SlotsManager;
            this.validator = engine.PoaHeaderValidator;
            this.chainState = engine.ChainState;
            this.modifiedFederation = new ModifiedFederation(this.Parent.Network, engine.FederationManager, engine.VotingManager);

            this.maxReorg = this.Parent.Network.Consensus.MaxReorgLength;
        }

        public override void Run(RuleContext context)
        {
            ChainedHeader currentHeader = context.ValidationContext.ChainedHeaderToValidate;

            // We're working with the federation members as after the previously connected block.
            List<IFederationMember> modifiedFederationMembers = this.modifiedFederation.GetFederationMembersAfterBlockConnected(currentHeader.Height - 1);

            var poaHeader = currentHeader.Header as PoABlockHeader;
            PubKey pubKey = this.slotsManager.GetFederationMemberForTimestamp(poaHeader.Time, modifiedFederationMembers).PubKey;

            if (this.validator.VerifySignature(pubKey, poaHeader))
            {
                this.Logger.LogDebug("Signature verified using updated federation.");
                return;
            }

            bool mightBeInsufficient = currentHeader.Height - this.chainState.ConsensusTip.Height > this.maxReorg;
            if (mightBeInsufficient)
            {
                // Mark header as insufficient to avoid banning the peer that presented it.
                // When we advance consensus we will be able to validate it.
                context.ValidationContext.InsufficientHeaderInformation = true;
            }

            this.Logger.LogTrace("(-)[INVALID_SIGNATURE]");
            PoAConsensusErrors.InvalidHeaderSignature.Throw();
        }
    }
}
