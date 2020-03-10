using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public enum EndorsementState
    {
        Proposed,
        Approved
    }

    public class EndorsementInfo
    {
        public EndorsementState State { get; private set; }

        public EndorsementInfo()
        {
            this.SetState(EndorsementState.Proposed);
        }

        public void SetState(EndorsementState state)
        {
            this.State = state;
        }
    }

    public interface IEndorsements
    {
        EndorsementInfo GetEndorsement(uint256 proposalId);
        EndorsementInfo RecordEndorsement(uint256 proposalId);
    }

    public class Endorsements : IEndorsements
    {
        private readonly Dictionary<uint256, EndorsementInfo> endorsements;

        public Endorsements()
        {
            this.endorsements = new Dictionary<uint256, EndorsementInfo>();
        }

        public EndorsementInfo GetEndorsement(uint256 proposalId)
        {
            this.endorsements.TryGetValue(proposalId, out EndorsementInfo info);

            return info;
        }

        public EndorsementInfo RecordEndorsement(uint256 proposalId)
        {
            var info = new EndorsementInfo();
            this.endorsements[proposalId] = info;

            return info;
        }
    }
}
