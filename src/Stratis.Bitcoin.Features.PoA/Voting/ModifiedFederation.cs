using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public interface IModifiedFederation
    {
        List<IFederationMember> GetModifiedFederation(int height);
    }

    public class ModifiedFederation : IModifiedFederation
    {
        private readonly Network network;
        private readonly IFederationManager federationManager;
        private readonly VotingManager votingManager;

        public ModifiedFederation(Network network, IFederationManager federationManager, VotingManager votingManager)
        {
            this.network = network;
            this.federationManager = federationManager;
            this.votingManager = votingManager;
        }

        public List<IFederationMember> GetModifiedFederation(int height)
        {
            if (!(this.network.Consensus.Options is PoAConsensusOptions poAConsensusOptions && poAConsensusOptions.VotingEnabled))
                return this.federationManager.GetFederationMembers();

            // Start at genesis when determining members for a block height potentially below the tip.
            List<IFederationMember> modifiedFederation = poAConsensusOptions.GenesisFederationMembers;

            foreach (Poll poll in this.votingManager.GetFinishedPolls()
                .Where(x => !x.IsExecuted && ((x.VotingData.Key == VoteKey.AddFederationMember) || (x.VotingData.Key == VoteKey.KickFederationMember)))
                .OrderBy(p => p.PollVotedInFavorBlockData.Height))
            {
                if ((height - poll.PollVotedInFavorBlockData.Height) <= this.network.Consensus.MaxReorgLength)
                    // Not applied yet. Since the polls are ordered by height we can skip the remaining polls as well.
                    break;

                IFederationMember federationMember = (this.network.Consensus.ConsensusFactory as PoAConsensusFactory).DeserializeFederationMember(poll.VotingData.Data);

                if (poll.VotingData.Key == VoteKey.AddFederationMember)
                    modifiedFederation.Add(federationMember);
                else if (poll.VotingData.Key == VoteKey.KickFederationMember)
                    modifiedFederation.Remove(federationMember);
            }

            return modifiedFederation;
        }
    }
}
