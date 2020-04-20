using System.Collections.Generic;
using CertificateAuthority;
using MembershipServices;
using NBitcoin;
using NBitcoin.Crypto;
using Org.BouncyCastle.X509;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementInfo
    {
        private readonly IOrganisationLookup organisationLookup;
        private readonly ICertificatePermissionsChecker permissionsChecker;
        private readonly Network network;
        private readonly MofNPolicyValidator validator;

        /// <summary>
        /// A basic policy definining a minimum number of endorsement signatures required for an organisation.
        /// </summary>
        public Dictionary<Organisation, int> Policy { get; }

        public EndorsementState State { get; private set; }

        public EndorsementInfo(Dictionary<Organisation, int> policy, IOrganisationLookup organisationLookup, ICertificatePermissionsChecker permissionsChecker, Network network)
        {
            this.organisationLookup = organisationLookup;
            this.permissionsChecker = permissionsChecker;
            this.network = network;
            this.Policy = policy;
            this.validator = new MofNPolicyValidator(this.Policy);
        }

        /// <summary>
        /// Extracts the sender address from the transaction, obtains its certificate from
        /// membership services, and extracts its organisation.
        /// </summary>
        /// <param name="transaction"></param>
        public void AddSignature(Transaction transaction)
        {
            (Organisation organisation, string sender) = this.organisationLookup.FromTransaction(transaction);
            
            this.AddSignature(organisation, sender);
        }

        public bool AddSignature(X509Certificate certificate, SignedProposalResponse signedProposalResponse)
        {
            // Verify the signature matches the peer's certificate.
            var signature = new ECDSASignature(signedProposalResponse.Endorsement.Signature);
            var pubKey = new PubKey(signedProposalResponse.Endorsement.PubKey);

            var signatureValid = this.permissionsChecker.CheckSignature(CaCertificatesManager.GetThumbprint(certificate), signature, pubKey, signedProposalResponse.ProposalResponse.GetHash());

            if (!signatureValid)
            {
                return false;
            }

            // Add the signature org + address to the policy state.
            (Organisation org, string sender) = this.organisationLookup.FromCertificate(certificate);

            AddSignature(org, sender);

            return true;
        }

        public void AddSignature(Organisation org, string address)
        {
            this.validator.AddSignature(org, address);

            if (this.Validate())
            {
                this.State = EndorsementState.Approved;
            }
        }

        /// <summary>
        /// Validate the transaction against the policy.
        /// </summary>
        /// <returns></returns>
        public bool Validate()
        {
            return this.validator.Valid;
        }
    }
}