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

    public class Endorsements : IEndorsements
    {
        private readonly IOrganisationLookup organisationLookup;
        private readonly ICertificatePermissionsChecker permissionsChecker;
        private readonly Network network;
        private readonly Dictionary<uint256, EndorsementInfo> endorsements;

        public Endorsements(IOrganisationLookup organisationLookup, ICertificatePermissionsChecker permissionsChecker, Network network)
        {
            this.organisationLookup = organisationLookup;
            this.permissionsChecker = permissionsChecker;
            this.network = network;
            this.endorsements = new Dictionary<uint256, EndorsementInfo>();
        }

        public EndorsementInfo GetEndorsement(uint256 proposalId)
        {
            this.endorsements.TryGetValue(proposalId, out EndorsementInfo info);

            return info;
        }

        public EndorsementInfo RecordEndorsement(uint256 proposalId, EndorsementPolicy endorsementPolicy)
        {
            var info = new EndorsementInfo(endorsementPolicy, this.organisationLookup, this.permissionsChecker, this.network);
            this.endorsements[proposalId] = info;

            return info;
        }
    }
}
