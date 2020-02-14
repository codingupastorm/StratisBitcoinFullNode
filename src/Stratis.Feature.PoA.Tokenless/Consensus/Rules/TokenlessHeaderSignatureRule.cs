using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
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
        private readonly IModifiedFederation modifiedFederation;

        public TokenlessHeaderSignatureRule(PoABlockHeaderValidator headerValidator, ISlotsManager slotsManager, IModifiedFederation modifiedFederation)
        {
            this.headerValidator = headerValidator;
            this.slotsManager = slotsManager;
            this.modifiedFederation = modifiedFederation;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Run(RuleContext context)
        {
            ChainedHeader currentHeader = context.ValidationContext.ChainedHeaderToValidate;

            // We're working with the federation members as after the previously connected block.
            List<IFederationMember> modifiedFederationMembers = this.modifiedFederation.GetFederationMembersAfterBlockConnected(currentHeader.Height - 1);

            var poaHeader = currentHeader.Header as PoABlockHeader;
            PubKey pubKey = this.slotsManager.GetFederationMemberForTimestamp(poaHeader.Time, modifiedFederationMembers).PubKey;

            if (this.headerValidator.VerifySignature(pubKey, poaHeader))
            {
                this.Logger.LogDebug("Signature verified using updated federation.");
            }

            bool mightBeInsufficient = currentHeader.Height - this.Parent.ChainState.ConsensusTip.Height > this.Parent.Network.Consensus.MaxReorgLength;
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