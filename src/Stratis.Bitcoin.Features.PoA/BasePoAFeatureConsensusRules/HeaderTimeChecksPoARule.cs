using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules
{
    /// <summary>
    /// Ensures the following: 
    /// <para>The timestamp of the current block is greater than the timestamp of the previous block.</para>
    /// <para>The timestamp is not more than the targetSpacing in seconds into the future.</para>
    /// <para>The timestamp is divisible by target spacing.</para>
    /// </summary>
    /// <seealso cref="HeaderValidationConsensusRule" />
    public class HeaderTimeChecksPoARule : HeaderValidationConsensusRule
    {
        /// <summary>Up to how many seconds headers's timestamp can be in the future to be considered valid.</summary>
        public const int MaxFutureDriftSeconds = 10;

        private readonly ISlotsManager slotsManager;

        public HeaderTimeChecksPoARule(ISlotsManager slotsManager)
        {
            this.slotsManager = slotsManager;
        }

        /// <inheritdoc />
        [NoTrace]
        public override void Initialize()
        {
            base.Initialize();
        }

        /// <inheritdoc />
        public override void Run(RuleContext context)
        {
            ChainedHeader currentChainedHeader = context.ValidationContext.ChainedHeaderToValidate;

            // Timestamp should be greater than the timestamp of the previous block.
            if (currentChainedHeader.Header.Time <= currentChainedHeader.Previous.Header.Time)
            {
                this.Logger.LogTrace("(-)[TIMESTAMP_INVALID_OLDER_THAN_PREV]");
                ConsensusErrors.TimeTooOld.Throw();
            }

            // Timestamp shouldn't be more than the current the time plus max future drift.
            long maxValidTime = this.Parent.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp() + MaxFutureDriftSeconds;
            if (currentChainedHeader.Header.Time > maxValidTime)
            {
                this.Logger.LogWarning("Peer presented a header with a timestamp that is too far into the future. Header was ignored." +
                                       " If you see this message a lot consider checking if your computer's time is correct.");
                this.Logger.LogTrace("(-)[TIMESTAMP_INVALID_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Timestamp should be divisible by target spacing.
            if (!this.slotsManager.IsValidTimestamp(currentChainedHeader.Header.Time))
            {
                this.Logger.LogTrace("(-)[TIMESTAMP_INVALID_NOT_DIVISBLE]");
                PoAConsensusErrors.InvalidHeaderTimestamp.Throw();
            }
        }
    }
}
