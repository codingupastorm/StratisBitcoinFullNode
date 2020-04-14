using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public enum EndorsementState
    {
        Proposed,
        Approved
    }

    public struct Organisation
    {
        public Organisation(string value)
        {
            this.Value = value;
        }

        public readonly string Value;

        public static explicit operator Organisation(string value)
        {
            return new Organisation(value);
        }

        public static implicit operator string(Organisation gas)
        {
            return gas.Value;
        }

        public override string ToString()
        {
            return this.Value;
        }
    }

    public class EndorsementInfo
    {
        /// <summary>
        /// A basic policy definining a minimum number of endorsement signatures required for an organisation.
        /// </summary>
        public Dictionary<Organisation, int> Policy { get; }

        public EndorsementState State { get; private set; }

        public EndorsementInfo()
        {
            // TODO remove this constructor once we are able to pass in the policy.
            this.SetState(EndorsementState.Proposed);
        }

        public EndorsementInfo(Dictionary<Organisation, int> policy)
        {
            this.Policy = policy;
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
