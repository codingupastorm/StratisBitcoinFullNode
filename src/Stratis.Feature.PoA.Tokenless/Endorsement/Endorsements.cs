using System.Collections.Generic;
using System.Linq;
using CertificateAuthority.Models;
using NBitcoin;
using Stratis.Features.PoA.ProtocolEncryption;

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
        EndorsementInfo RecordEndorsement(uint256 proposalId);
    }

    public class Endorsements : IEndorsements
    {
        private readonly Dictionary<uint256, EndorsementInfo> endorsements;
        private List<CertificateInfoModel> knownCertificates;

        public Endorsements()
        {
            //this.knownCertificates = certificates;
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
