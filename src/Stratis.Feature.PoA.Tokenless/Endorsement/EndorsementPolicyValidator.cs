using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Core.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    /// <summary>
    /// Checks that an endorsement's policy is met. Doesn't check signatures.
    /// </summary>
    public class EndorsementPolicyValidator
    {
        private readonly EndorsementPolicy policy;

        /// <summary>
        ///  A list of signatures from individuals permitted by the access control list.
        /// </summary>
        private readonly HashSet<string> policyValidationState;

        public EndorsementPolicyValidator(EndorsementPolicy policy)
        {
            this.policy = policy;
            this.policyValidationState = new HashSet<string>();
        }

        public void AddSignature(Organisation org, string address)
        {
            // TODO: Do we need to check somewhere that the signature is correct when from a thumbprint rather than organisation?

            if (!this.policy.AccessList.Organisations.Contains(org))
                return;

            // Don't add same signature twice
            if (ContainsSignature(address))
                return;

            this.policyValidationState.Add(address);
        }

        private bool ContainsSignature(string address)
        {
            return this.policyValidationState.Contains(address);
        }

        private int GetUniqueSignatureCount()
        {
            return this.policyValidationState.Count;
        }

        /// <summary>
        /// Returns addresses that match the validation policy.
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<string> GetAddresses()
        {
            return this.policyValidationState.ToList();
        }

        public bool Valid 
        {
            get
            {
                return this.policyValidationState.Count >= this.policy.RequiredSignatures;
            }
        }
    }
}