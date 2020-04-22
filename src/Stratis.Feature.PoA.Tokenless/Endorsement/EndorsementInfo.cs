using System.Collections.Generic;
using System.Linq;
using CertificateAuthority;
using NBitcoin;
using NBitcoin.Crypto;
using Org.BouncyCastle.X509;
using Stratis.SmartContracts.Core.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementInfo
    {
        private readonly IOrganisationLookup organisationLookup;
        private readonly ICertificatePermissionsChecker permissionsChecker;
        private readonly Network network;
        private readonly MofNPolicyValidator validator;
        private readonly Dictionary<string, SignedProposalResponse> signedProposals;

        /// <summary>
        /// A basic policy definining a minimum number of endorsement signatures required for an organisation.
        /// </summary>
        public EndorsementPolicy Policy { get; }

        public EndorsementState State { get; private set; }

        public EndorsementInfo(EndorsementPolicy policy, IOrganisationLookup organisationLookup, ICertificatePermissionsChecker permissionsChecker, Network network)
        {
            this.organisationLookup = organisationLookup;
            this.permissionsChecker = permissionsChecker;
            this.network = network;
            this.Policy = policy;
            this.validator = new MofNPolicyValidator(this.Policy);

            // To prevent returning proposals that were signed correctly but do not match the policy,
            // we should keep track of signed proposals from all addresses and filter them by the
            // valid addresses in the policy.
            this.signedProposals = new Dictionary<string, SignedProposalResponse>();
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

            this.signedProposals[sender] = signedProposalResponse;

            return true;
        }

        public IReadOnlyList<SignedProposalResponse> GetValidProposalResponses()
        {
            // Returns signed proposal responses that match current proposals returned by addresses that meet the policy,
            return this.validator.GetValidAddresses()
                .Where(a => this.signedProposals.ContainsKey(a))
                .Select(a => this.signedProposals[a])
                .ToList();
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