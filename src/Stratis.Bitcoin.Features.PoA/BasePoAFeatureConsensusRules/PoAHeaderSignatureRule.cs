﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA.Voting;

namespace Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules
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

        private bool votingEnabled;

        private VotingManager votingManager;

        private IFederationManager federationManager;

        private IChainState chainState;

        private PoAConsensusFactory consensusFactory;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            var rulesEngine = this.Parent as PoAConsensusRuleEngine;

            this.slotsManager = rulesEngine.SlotsManager;
            this.validator = rulesEngine.PoaHeaderValidator;
            this.votingManager = rulesEngine.VotingManager;
            this.federationManager = rulesEngine.FederationManager;
            this.chainState = rulesEngine.ChainState;
            this.consensusFactory = (PoAConsensusFactory)this.Parent.Network.Consensus.ConsensusFactory;

            this.maxReorg = this.Parent.Network.Consensus.MaxReorgLength;
            this.votingEnabled = ((PoAConsensusOptions)this.Parent.Network.Consensus.Options).VotingEnabled;
        }

        public override void Run(RuleContext context)
        {
            var poaHeader = context.ValidationContext.ChainedHeaderToValidate.Header as PoABlockHeader;

            PubKey pubKey = this.slotsManager.GetFederationMemberForTimestamp(poaHeader.Time).PubKey;

            if (!this.validator.VerifySignature(pubKey, poaHeader))
            {
                // If voting is enabled, it is possible that the federation was modified and another federation member signed
                // the header. Since voting changes are only applied after [max reorg] blocks have passed, we can tell exactly
                // how the federation will look like, [max reorg] blocks ahead. The code below tries to construct the federation that is
                // expected to exist at the moment the block that corresponds to header being validated was produced. Then
                // this federation is used to estimate who was expected to sign a block and then the signature is verified.
                if (this.votingEnabled)
                {
                    ChainedHeader currentHeader = context.ValidationContext.ChainedHeaderToValidate;

                    bool mightBeInsufficient = currentHeader.Height - this.chainState.ConsensusTip.Height > this.maxReorg;

                    List<IFederationMember> modifiedFederation = this.federationManager.GetFederationMembers();

                    foreach (Poll poll in this.votingManager.GetFinishedPolls().Where(x => !x.IsExecuted &&
                        ((x.VotingData.Key == VoteKey.AddFederationMember) || (x.VotingData.Key == VoteKey.KickFederationMember))))
                    {
                        if (currentHeader.Height - poll.PollVotedInFavorBlockData.Height <= this.maxReorg)
                            // Not applied yet.
                            continue;

                        IFederationMember federationMember = this.consensusFactory.DeserializeFederationMember(poll.VotingData.Data);

                        if (poll.VotingData.Key == VoteKey.AddFederationMember)
                            modifiedFederation.Add(federationMember);
                        else if (poll.VotingData.Key == VoteKey.KickFederationMember)
                            modifiedFederation.Remove(federationMember);
                    }

                    pubKey = this.slotsManager.GetFederationMemberForTimestamp(poaHeader.Time, modifiedFederation).PubKey;

                    if (this.validator.VerifySignature(pubKey, poaHeader))
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
