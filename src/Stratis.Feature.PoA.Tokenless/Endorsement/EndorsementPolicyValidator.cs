using System.Collections.Generic;
using System.Linq;
using CertificateAuthority.Models;
using NBitcoin;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using ByteArrayComparer = Stratis.Bitcoin.Utilities.ByteArrayComparer;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementPolicyValidator
    {
        bool Validate(ReadWriteSet rws, IEnumerable<Endorsement> endorsements);
    }

    /// <summary>
    /// Validates that an endorsement policy is satisfied for a transaction to an already-deployed contract.
    /// </summary>
    public class EndorsementPolicyValidator : IEndorsementPolicyValidator
    {
        private readonly ICertificatesManager certificatesManager;
        private readonly IOrganisationLookup organisationLookup;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly ByteArrayComparer byteArrayComparer;

        public EndorsementPolicyValidator(ICertificatesManager certificatesManager, IOrganisationLookup organisationLookup, IStateRepositoryRoot stateRoot)
        {
            this.certificatesManager = certificatesManager;
            this.organisationLookup = organisationLookup;
            this.stateRoot = stateRoot;
            this.byteArrayComparer = new ByteArrayComparer();
        }

        /// <summary>
        /// Validates that the policy for this RWS is matched by the given endorsements.
        /// </summary>
        /// <param name="rws"></param>
        /// <param name="endorsements"></param>
        /// <returns></returns>
        public bool Validate(ReadWriteSet rws, IEnumerable<Endorsement> endorsements)
        {
            // RWS has no reads/writes, should always be OK?
            if (rws.ContractAddress == null)
                return true;

            EndorsementPolicy policy = this.stateRoot.GetPolicy(rws.ContractAddress);

            // No policy means no contract at this address, or some other error.
            if (policy == null)
                return false;

            List<CertificateInfoModel> certs = this.certificatesManager.GetAllCertificates();

            // This is currently the only possible policy type, but in the future if more are added we will need to
            // add a PolicyValidatorFactory that returns the correct policy for a particular EndorsementPolicy subclass.
            var policyValidator = new MofNPolicyValidator(policy.ToDictionary());

            foreach (Endorsement endorsement in endorsements)
            {
                var pubKey = new PubKey(endorsement.PubKey);

                CertificateInfoModel certificateInfoModel = certs
                    .FirstOrDefault(c =>
                        this.byteArrayComparer.Equals(c.TransactionSigningPubKeyHash, pubKey.Hash.ToBytes()));

                if (certificateInfoModel != null)
                {
                    (Organisation organisation, string sender) = this.organisationLookup.FromCertificate(certificateInfoModel.ToCertificate());
                    policyValidator.AddSignature(organisation, sender);
                }
            }

            return policyValidator.Valid;
        }
    }
}