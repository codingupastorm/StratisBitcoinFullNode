using System.Linq;
using Org.BouncyCastle.X509;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// Validation methods to determine if a RWS can be accessed by a member of the given organisation using the
    /// endorsement policy associated with the RWS' contract address.
    /// </summary>
    public class ReadWriteSetPolicyValidator
    {
        private readonly ICertificatesManager certificatesManager;
        private readonly IStateRepository stateRepository;

        public ReadWriteSetPolicyValidator(ICertificatesManager certificatesManager, IStateRepositoryRoot stateRepository)
        {
            this.certificatesManager = certificatesManager;
            this.stateRepository = stateRepository;
        }

        public bool ClientCanAccessPrivateData(ReadWriteSet readWriteSet)
        {
            return this.OrganisationCanAccessPrivateData((Organisation) this.certificatesManager.ClientCertificate.GetOrganisation(), readWriteSet);
        }

        public bool OrganisationCanAccessPrivateData(Organisation organisation, ReadWriteSet readWriteSet)
        {
            // Check if we are meant to have the data. 
            WriteItem write = readWriteSet.Writes.First(x => x.IsPrivateData);
            EndorsementPolicy policy = this.stateRepository.GetPolicy(write.ContractAddress);

            if (policy == null)
            {
                return false;
            }

            return policy.Organisation == organisation;
        }

        public bool OrganisationCanAccessPrivateData(X509Certificate certificate, ReadWriteSet readWriteSet)
        {
            return this.OrganisationCanAccessPrivateData((Organisation) certificate.GetOrganisation(), readWriteSet);
        }
    }
}