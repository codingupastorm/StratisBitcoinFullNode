using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public enum EndorsementState
    {
        Proposed,
        Approved
    }

    public interface IEndorsements
    {
        EndorsementInfo GetEndorsement(uint256 proposalId);
        EndorsementInfo RecordEndorsement(uint256 proposalId, EndorsementPolicy policy);
    }

    /// <summary>
    /// Maintains the state of received endorsement responses for each proposal. Currently in memory only.
    /// </summary>
    public class Endorsements : IEndorsements
    {
        private readonly IOrganisationLookup organisationLookup;
        private readonly IEndorsementSignatureValidator endorsementSignatureValidator;
        private readonly Dictionary<uint256, EndorsementInfo> endorsements;

        public Endorsements(IOrganisationLookup organisationLookup, IEndorsementSignatureValidator endorsementSignatureValidator)
        {
            this.organisationLookup = organisationLookup;
            this.endorsementSignatureValidator = endorsementSignatureValidator;
            this.endorsements = new Dictionary<uint256, EndorsementInfo>();
        }

        public EndorsementInfo GetEndorsement(uint256 proposalId)
        {
            this.endorsements.TryGetValue(proposalId, out EndorsementInfo info);

            return info;
        }

        public EndorsementInfo RecordEndorsement(uint256 proposalId, EndorsementPolicy endorsementPolicy)
        {
            if(this.endorsements.ContainsKey(proposalId))
            {
                return this.endorsements[proposalId];
            }

            var info = new EndorsementInfo(endorsementPolicy, this.organisationLookup, this.endorsementSignatureValidator);

            this.endorsements[proposalId] = info;

            return info;
        }
    }
}
