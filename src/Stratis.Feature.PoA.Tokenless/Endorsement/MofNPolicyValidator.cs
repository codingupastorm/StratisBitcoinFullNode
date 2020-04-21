using System.Collections.Generic;
using Stratis.SmartContracts.Core.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class MofNPolicyValidator
    {
        private readonly Dictionary<Organisation, int> policy;

        /// <summary>
        /// Keeps track of the current validation state of the policy. Once the minimum number of unique signatures has been met per organisation.
        ///
        /// Does not validate that signatures are correct.
        /// </summary>
        private readonly Dictionary<Organisation, HashSet<string>> policyValidationState = new Dictionary<Organisation, HashSet<string>>();

        public MofNPolicyValidator(Dictionary<Organisation, int> policy)
        {
            this.policy = policy;
        }

        public void AddSignature(Organisation org, string address)
        {
            InitializeHashSet(org);

            // Don't add same signature twice
            if (ContainsSignature(org, address))
                return;

            this.policyValidationState[org].Add(address);
        }

        private void InitializeHashSet(Organisation org)
        {
            if(!this.policyValidationState.ContainsKey(org))
                this.policyValidationState[org] = new HashSet<string>();
        }

        private bool ContainsSignature(Organisation org, string address)
        {
            return this.policyValidationState.ContainsKey(org)
                   && this.policyValidationState[org].Contains(address);
        }

        private int GetUniqueSignatureCount(Organisation org)
        {
            if (!this.policyValidationState.ContainsKey(org)
                || this.policyValidationState[org] == null)
                return 0;

            return this.policyValidationState[org].Count;
        }

        /// <summary>
        /// Returns addresses that match the validation policy.
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<string> GetValidAddresses()
        {
            var result = new List<string>();

            foreach ((Organisation org, int _) in this.policy)
            {
                result.AddRange(this.policyValidationState[org]);
            }

            return result;
        }

        public bool Valid 
        {
            get
            {
                foreach ((Organisation org, int requiredSigCount) in this.policy)
                {
                    if (GetUniqueSignatureCount(org) < requiredSigCount)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}