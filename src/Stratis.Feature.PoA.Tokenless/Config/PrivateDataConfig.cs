using CSharpFunctionalExtensions;

namespace Stratis.Feature.PoA.Tokenless.Config
{
    /// <summary>
    /// The configuration to apply to private data dissemination and access.
    ///
    /// TODO - Implement policy validation methods
    /// TODO - Distribute policy with contract deployment transaction
    /// TODO - Add a way to query policies to avoid trawling through all blocks (policy cache/store?) with a way to update existing configs
    /// TODO - Disseminate private data and enforce policy
    /// </summary>
    public class PrivateDataConfig
    {
        public const string EmptyFieldNameError = "Field name is not a valid identifier.";
        public const string FieldNameError = "Field name is not a valid identifier.";
        public const string PeerCountError = "Maximum peer count cannot be less than minimum peer count.";
        public const string MinimumPeerCountLessThanZeroError = "Minimum peer count cannot be less than zero.";

        public PrivateDataConfig(string name, PolicyInfo policyInfo, int blockToLive, int minimumPeerCount,
            int maximumPeerCount, bool memberOnlyRead, bool memberOnlyWrite)
        {
            this.Name = name;
            this.PolicyInfo = policyInfo;
            this.BlockToLive = blockToLive;
            this.MinimumPeerCount = minimumPeerCount;
            this.MaximumPeerCount = maximumPeerCount;
            this.MemberOnlyRead = memberOnlyRead;
            this.MemberOnlyWrite = memberOnlyWrite;
        }

        /// <summary>
        /// The name of the private data field in the contract.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The policy defining who can access the private data.
        /// </summary>
        public PolicyInfo PolicyInfo { get; }

        /// <summary>
        /// The minimum number of peers the private data will be sent to upon endorsement.
        /// The endorsement will fail if dissemination to this number of peers is not achieved.
        /// </summary>
        public int MinimumPeerCount { get; }

        /// <summary>
        /// The maximum number of peers the private data will be sent to upon endorsement.
        /// </summary>
        public int MaximumPeerCount { get; }

        /// <summary>
        /// The number of blocks after which the collection expires and the data is purged.
        /// To keep data indefinitely, set this property to 0.
        /// </summary>
        public int BlockToLive { get; }

        /// <summary>
        /// Specifies whether only members can read the private data.
        /// </summary>
        public bool MemberOnlyRead { get; }

        /// <summary>
        /// Specifies whether only members can write the private data.
        /// </summary>
        public bool MemberOnlyWrite { get; }

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(this.Name))
                return Result.Failure(EmptyFieldNameError);

            if(!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(this.Name))
                return Result.Failure(FieldNameError);

            if (this.MaximumPeerCount < this.MinimumPeerCount)
                return Result.Failure(PeerCountError);

            if(this.MinimumPeerCount < 0)
                return Result.Failure(MinimumPeerCountLessThanZeroError);

            return this.PolicyInfo.Validate();
        }
    }
}
