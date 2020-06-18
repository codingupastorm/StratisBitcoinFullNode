using System.Linq;
using CertificateAuthority;
using MembershipServices;
using Org.BouncyCastle.X509;
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
        private readonly IMembershipServicesDirectory membershipServices;
        private readonly IStateRepository stateRepository;

        public ReadWriteSetPolicyValidator(IMembershipServicesDirectory membershipServices, IStateRepositoryRoot stateRepository)
        {
            this.membershipServices = membershipServices;
            this.stateRepository = stateRepository;
        }

        public bool ClientCanAccessPrivateData(ReadWriteSet readWriteSet)
        {
            return CertificateCanAccessPrivateData(this.membershipServices.ClientCertificate, readWriteSet);
        }

        public bool OrganisationAndThumbprintCanAccessPrivateData(Organisation organisation, string thumbprint, ReadWriteSet readWriteSet)
        {
            // Check if we are meant to have the data. 

            WriteItem write = readWriteSet.Writes.First(x => x.IsPrivateData);
            EndorsementPolicy policy = this.stateRepository.GetPolicy(write.ContractAddress);

            if (policy == null)
            {
                return false;
            }

            return policy.AccessList.Organisations.Contains(organisation)
                || policy.AccessList.Thumbprints.Contains(thumbprint);
        }

        public bool CertificateCanAccessPrivateData(X509Certificate certificate, ReadWriteSet readWriteSet)
        {
            Organisation organisation = (Organisation) certificate.GetOrganisation();
            string thumbprint = CaCertificatesManager.GetThumbprint(certificate);

            return this.OrganisationAndThumbprintCanAccessPrivateData(organisation, thumbprint, readWriteSet);
        }
    }
}