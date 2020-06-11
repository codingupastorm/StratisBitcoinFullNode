using System.Collections.Generic;
using MembershipServices;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;

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
        private readonly IMembershipServicesDirectory membershipServices;
        private readonly IOrganisationLookup organisationLookup;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IEndorsementSignatureValidator signatureValidator;

        public EndorsementPolicyValidator(
            IMembershipServicesDirectory membershipServices, 
            IOrganisationLookup organisationLookup, 
            IStateRepositoryRoot stateRoot,
            IEndorsementSignatureValidator signatureValidator)
        {
            this.membershipServices = membershipServices;
            this.organisationLookup = organisationLookup;
            this.stateRoot = stateRoot;
            this.signatureValidator = signatureValidator;
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

            // This is currently the only possible policy type, but in the future if more are added we will need to
            // add a PolicyValidatorFactory that returns the correct policy for a particular EndorsementPolicy subclass.
            var policyValidator = new EndorsementPolicySignatureValidator(policy);

            foreach (Endorsement endorsement in endorsements)
            {
                var pubKey = new PubKey(endorsement.PubKey);

                X509Certificate cert = this.membershipServices.GetCertificateForTransactionSigningPubKeyHash(pubKey.Hash.ToBytes());

                if (cert != null)
                {
                    (Organisation organisation, string sender) = this.organisationLookup.FromCertificate(cert);
                    
                    // It's possible that we get an endorsement which contains some invalid signatures but still meets the policy.
                    // Therefore we should only add valid signatures to the policy.
                    if (this.signatureValidator.Validate(endorsement, rws.ToJsonEncodedBytes()))
                    {
                        policyValidator.AddSignature(organisation, sender);
                    }
                }
            }

            return policyValidator.Valid;
        }
    }
}