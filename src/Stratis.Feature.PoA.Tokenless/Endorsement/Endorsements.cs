using System.Collections.Generic;
using System.Linq;
using CertificateAuthority.Models;
using NBitcoin;
using Stratis.Features.PoA.ProtocolEncryption;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public enum EndorsementState
    {
        Proposed,
        Approved
    }

    public struct Organisation
    {
        public Organisation(string value)
        {
            this.Value = value;
        }

        public readonly string Value;

        public static explicit operator Organisation(string value)
        {
            return new Organisation(value);
        }

        public static implicit operator string(Organisation gas)
        {
            return gas.Value;
        }

        public override string ToString()
        {
            return this.Value;
        }
    }

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
    public class EndorsementInfo
    {
        /// <summary>
        /// A basic policy definining a minimum number of endorsement signatures required for an organisation.
        /// </summary>
        public Dictionary<Organisation, int> Policy { get; }

        public EndorsementState State { get; private set; }

        public EndorsementInfo()
        {
            // TODO remove this constructor once we are able to pass in the policy.
            this.SetState(EndorsementState.Proposed);
        }

        public EndorsementInfo(Dictionary<Organisation, int> policy)
        {
            this.Policy = policy;
        }

        public void SetState(EndorsementState state)
        {
            this.State = state;
        }
    }

    public interface IEndorsements
    {
        EndorsementInfo GetEndorsement(uint256 proposalId);
        EndorsementInfo RecordEndorsement(uint256 proposalId);
    }

    public class Endorsements : IEndorsements
    {
        private readonly Dictionary<uint256, EndorsementInfo> endorsements;

        public Endorsements()
        {
            this.endorsements = new Dictionary<uint256, EndorsementInfo>();
        }

        public EndorsementInfo GetEndorsement(uint256 proposalId)
        {
            this.endorsements.TryGetValue(proposalId, out EndorsementInfo info);

            return info;
        }

        public EndorsementInfo RecordEndorsement(uint256 proposalId)
        {
            var info = new EndorsementInfo();
            this.endorsements[proposalId] = info;

            return info;
        }
    }
}
