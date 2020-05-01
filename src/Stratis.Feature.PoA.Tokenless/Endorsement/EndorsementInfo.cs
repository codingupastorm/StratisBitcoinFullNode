using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.X509;
using Stratis.SmartContracts.Core.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    /// <summary>
    /// Combines signature validation with m-of-n validation for proposal responses.
    /// </summary>
    public class EndorsementInfo
    {
        private readonly IOrganisationLookup organisationLookup;
        private readonly IEndorsementSignatureValidator endorsementSignatureValidator;
        private readonly MofNPolicyValidator validator;
        private readonly Dictionary<string, SignedProposalResponse> signedProposals;

        /// <summary>
        /// A basic policy definining a minimum number of endorsement signatures required for an organisation.
        /// </summary>
        public EndorsementPolicy Policy { get; }

        public EndorsementState State { get; private set; }

        public EndorsementInfo(EndorsementPolicy policy, IOrganisationLookup organisationLookup, IEndorsementSignatureValidator endorsementSignatureValidator)
        {
            this.organisationLookup = organisationLookup;
            this.Policy = policy;
            this.endorsementSignatureValidator = endorsementSignatureValidator;
            this.validator = new MofNPolicyValidator(this.Policy.ToDictionary());

            // To prevent returning proposals that were signed correctly but do not match the policy,
            // we should keep track of signed proposals from all addresses and filter them by the
            // valid addresses in the policy.
            this.signedProposals = new Dictionary<string, SignedProposalResponse>();
        }

        public bool AddSignature(X509Certificate certificate, SignedProposalResponse signedProposalResponse)
        {
            // Verify the signature matches the peer's certificate.
            if (!this.endorsementSignatureValidator.Validate(signedProposalResponse.Endorsement, signedProposalResponse.ProposalResponse.ToBytes()))
            {
                return false;
            }

            //// Add the signature org + address to the policy state.
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

        private void AddSignature(Organisation org, string address)
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