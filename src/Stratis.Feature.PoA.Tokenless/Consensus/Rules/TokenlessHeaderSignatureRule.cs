using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules
{
    /// <summary>
    /// Estimates which public key should be used for timestamp of a header being
    /// validated and uses this public key to verify header's signature.
    /// </summary>
    public sealed class TokenlessHeaderSignatureRule : HeaderValidationConsensusRule
    {
        private readonly PoABlockHeaderValidator headerValidator;
        private readonly ISlotsManager slotsManager;

        public TokenlessHeaderSignatureRule(PoABlockHeaderValidator headerValidator, ISlotsManager slotsManager)
        {
            this.headerValidator = headerValidator;
            this.slotsManager = slotsManager;
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
                this.Logger.LogTrace("(-)[INVALID_SIGNATURE]");
                PoAConsensusErrors.InvalidHeaderSignature.Throw();
            }
        }
    }
}