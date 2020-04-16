using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementInfo
    {
        private readonly IOrganisationLookup organisationLookup;
        private readonly MofNPolicyValidator validator;

        /// <summary>
        /// A basic policy definining a minimum number of endorsement signatures required for an organisation.
        /// </summary>
        public Dictionary<Organisation, int> Policy { get; }

        public EndorsementState State { get; private set; }

        public EndorsementInfo(Dictionary<Organisation, int> policy, IOrganisationLookup organisationLookup)
        {
            this.organisationLookup = organisationLookup;
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